// <copyright file="ConnectionWatchdog.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Monitors the connection to the Soulseek network and reconnects with exponential backoff, if necessary.
    /// </summary>
    /// <remarks>
    ///     This class is intended to be Started either at application startup or when the connection is disconnected, and
    ///     stopped when the application is connected again; it doesn't "run" all the time.
    /// </remarks>
    public class ConnectionWatchdog
    {
        private static readonly int ReconnectMaxDelayMilliseconds = 300000; // 5 minutes

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConnectionWatchdog"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="state"></param>
        public ConnectionWatchdog(
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<State> state)
        {
            Client = soulseekClient;
            OptionsMonitor = optionsMonitor;
            State = state;

            WatchdogTimer = new System.Timers.Timer()
            {
                Interval = 100,
                AutoReset = true,
                Enabled = false,
            };

            WatchdogTimer.Elapsed += (sender, args) => _ = AttemptReconnect();

            OptionsMonitor.OnChange(options => OptionsChanged(options));
        }

        /// <summary>
        ///     Gets a value indicating whether the watchdog is monitoring the server connection.
        /// </summary>
        public bool IsEnabled => WatchdogTimer.Enabled;

        private ISoulseekClient Client { get; }
        private bool Disposed { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private IStateMonitor<State> State { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private System.Timers.Timer WatchdogTimer { get; }
        private CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Initializes the watchdog and makes the initial connection to the server.
        /// </summary>
        /// <remarks>This should be called at application startup.</remarks>
        public virtual void Start()
        {
            WatchdogTimer.Enabled = true;
            _ = AttemptReconnect(attempts: 0);
        }

        /// <summary>
        ///     Starts monitoring the server connection.
        /// </summary>
        /// <remarks>This should be called when the connection is disconnected.</remarks>
        public void Restart()
        {
            WatchdogTimer.Enabled = true;
            _ = AttemptReconnect(attempts: 1);
        }

        /// <summary>
        ///     Stops monitoring the server connection.
        /// </summary>
        /// <param name="abortReconnect">A value indicating whether to abort an ongoing reconnect attempt.</param>
        /// <remarks>This should be called when the application is reasonably certain that the connection is connected.</remarks>
        public virtual void Stop(bool abortReconnect = false)
        {
            WatchdogTimer.Enabled = false;

            // note: the connect event fires before the connection logic is complete, so there is a chain of events
            // that leads to the application being connected and then immediately disconnected if this CTS is cancelled
            // when the watchdog is stopped due to sucessful connection. pass the cancelReconnect flag only when the user
            // wants to abort the reconnect logic.
            if (abortReconnect)
            {
                CancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    WatchdogTimer?.Dispose();
                    CancellationTokenSource?.Dispose();
                }

                Disposed = true;
            }
        }

        private void OptionsChanged(Options options)
        {
            // it's possible that a user begins changing connection settings in an attempt to get the client to connect
            // if they edit the options _at all_, assume this is the case and restart the retry loop from the beginning
            if (IsEnabled)
            {
                Log.Information("Options changed, restarting (re)connection process...");
                Restart();
            }
        }

        private async Task AttemptReconnect(int attempts = 1)
        {
            // semaphore is obtained here and released only when the reconnect attempt is complete, so
            // one and only one invocation of this can run at a time
            if (await SyncRoot.WaitAsync(0))
            {
                try
                {
                    // go until we connect and break, or something stops the watchdog
                    while (IsEnabled)
                    {
                        // bail out if we're already connected. it's possible that something else outside of the watchdog
                        // connects the client. highly unlikely but that might change if someone inadvertently adds some logic.
                        if (State.CurrentValue.Server.IsConnected)
                        {
                            return;
                        }

                        CancellationTokenSource = new CancellationTokenSource();

                        if (attempts > 0)
                        {
                            var (delay, jitter) = Compute.ExponentialBackoffDelay(
                                iteration: attempts,
                                maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

                            var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
                            Log.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before attempting to reconnect");
                            await Task.Delay(delay + jitter, cancellationToken: CancellationTokenSource.Token);

                            Log.Information("Attempting to reconnect to the Soulseek server (#{Attempts})...", attempts);
                        }
                        else
                        {
                            Log.Information("Attempting to connect to the Soulseek server...");
                        }

                        try
                        {
                            // reconnect with the latest configuration values we have for username and password, instead of the
                            // options that were captured at startup. if a user has updated these values prior to the disconnect,
                            // the changes will take effect now.
                            var opt = Options.CurrentValue.Soulseek;

                            // note: cancelling the CTS before the connect logic is fully complete (e.g. reacting to the connect event)
                            // will cause the client to connect, then disconnect immediately
                            await Client.ConnectAsync(
                                address: opt.Address,
                                port: opt.Port,
                                username: opt.Username,
                                password: opt.Password,
                                cancellationToken: CancellationTokenSource.Token);

                            break;
                        }
                        catch (Exception ex)
                        {
                            attempts++;
                            Log.Error("Failed to reconnect: {Message}", ex.Message);
                        }
                    }
                }
                finally
                {
                    CancellationTokenSource?.Dispose();
                    SyncRoot.Release();
                }
            }
        }
    }
}
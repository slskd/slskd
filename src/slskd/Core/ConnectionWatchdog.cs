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
    ///     Monitors the connection to the Soulseek server and reconnects with exponential backoff, if necessary.
    /// </summary>
    public interface IConnectionWatchdog : IDisposable
    {
        /// <summary>
        ///     Gets a value indicating whether the watchdog is monitoring the server connection.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        ///     Starts monitoring the server connection.
        /// </summary>
        /// <remarks>This should be called when the connection is disconnected.</remarks>
        void Start();

        /// <summary>
        ///     Stops monitoring the server connection.
        /// </summary>
        /// <remarks>This should be called when the application is reasonably certain that the connection is connected.</remarks>
        void Stop();
    }

    /// <summary>
    ///     Monitors the connection to the Soulseek network and reconnects with exponential backoff, if necessary.
    /// </summary>
    public class ConnectionWatchdog : IConnectionWatchdog
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
            Options = optionsMonitor;
            State = state;

            WatchdogTimer = new System.Timers.Timer()
            {
                Interval = 100,
                AutoReset = true,
                Enabled = false,
            };

            WatchdogTimer.Elapsed += (sender, args) => _ = AttemptReconnect();
        }

        /// <summary>
        ///     Gets a value indicating whether the watchdog is monitoring the server connection.
        /// </summary>
        public bool IsEnabled => WatchdogTimer.Enabled;

        private ISoulseekClient Client { get; }
        private bool Disposed { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> Options { get; set; }
        private IStateMonitor<State> State { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private System.Timers.Timer WatchdogTimer { get; }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Starts monitoring the server connection.
        /// </summary>
        /// <remarks>This should be called when the connection is disconnected.</remarks>
        public void Start()
        {
            WatchdogTimer.Enabled = true;
            _ = AttemptReconnect();
        }

        /// <summary>
        ///     Stops monitoring the server connection.
        /// </summary>
        /// <remarks>This should be called when the application is reasonably certain that the connection is connected.</remarks>
        public void Stop() => WatchdogTimer.Enabled = false;

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
                }

                Disposed = true;
            }
        }

        private async Task AttemptReconnect()
        {
            if (await SyncRoot.WaitAsync(0))
            {
                try
                {
                    if (State.CurrentValue.Server.IsConnected)
                    {
                        return;
                    }

                    var attempts = 1;

                    while (true)
                    {
                        var (delay, jitter) = Compute.ExponentialBackoffDelay(
                            iteration: attempts,
                            maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

                        var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
                        Log.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before reconnecting");
                        await Task.Delay(delay + jitter);

                        Log.Information("Attempting to reconnect (#{Attempts})...", attempts);

                        try
                        {
                            // reconnect with the latest configuration values we have for username and password, instead of the
                            // options that were captured at startup. if a user has updated these values prior to the disconnect,
                            // the changes will take effect now.
                            await Client.ConnectAsync(Options.CurrentValue.Soulseek.Username, Options.CurrentValue.Soulseek.Password);
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
                    SyncRoot.Release();
                }
            }
        }
    }
}
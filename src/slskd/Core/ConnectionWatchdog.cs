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
    using slskd.Integrations.VPN;
    using Soulseek;

    /// <summary>
    ///     Monitors the connection to the Soulseek network and reconnects with exponential backoff, if necessary.
    /// </summary>
    /// <remarks>
    ///     This class is intended to be Start()ed either at application startup or when the connection is disconnected, and
    ///     Stop()ed when the application is connected again; it doesn't "run" all the time because there are cases where
    ///     a user has manually disconnected, was kicked by another login somewhere, etc. where we don't want to reconnect.
    /// </remarks>
    public class ConnectionWatchdog
    {
        private static readonly int ReconnectMaxDelayMilliseconds = 300000; // 5 minutes

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConnectionWatchdog"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="vpnService"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="optionsAtStartup"></param>
        /// <param name="applicationState"></param>
        public ConnectionWatchdog(
            ISoulseekClient soulseekClient,
            VPNService vpnService,
            IOptionsMonitor<Options> optionsMonitor,
            OptionsAtStartup optionsAtStartup,
            IManagedState<State> applicationState)
        {
            Client = soulseekClient;
            VPN = vpnService;
            OptionsMonitor = optionsMonitor;
            OptionsAtStartup = optionsAtStartup;
            ApplicationState = applicationState;

            WatchdogTimer = new System.Timers.Timer()
            {
                Interval = 100,
                AutoReset = true,
                Enabled = false,
            };

            // the timer is used here to ensure that we keep trying if the connection logic fails for some reason. i'm questioning
            // this at the moment and may continue to do so
            WatchdogTimer.Elapsed += (sender, args) => _ = AttemptConnection(source: nameof(WatchdogTimer));

            OptionsMonitor.OnChange(options => OptionsChanged(options));
        }

        /// <summary>
        ///     Gets a value indicating whether the watchdog is monitoring the server connection.
        /// </summary>
        public bool IsEnabled => WatchdogTimer.Enabled;

        /// <summary>
        ///     Gets a value indicating whether the watchdog is actively attempting to connect.
        /// </summary>
        public bool IsAttemptingConnection => SyncRoot.CurrentCount == 0;

        /// <summary>
        ///     Gets a value indicating whether the watchdog is waiting for the VPN client to connect.
        /// </summary>
        public bool IsAwaitingVpn { get; private set; }

        private ISoulseekClient Client { get; }
        private VPNService VPN { get; }
        private bool Disposed { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private OptionsAtStartup OptionsAtStartup { get; set; }
        private IManagedState<State> ApplicationState { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private object StateLock { get; } = new object();
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
            if (OptionsAtStartup.Integration.Vpn.Enabled)
            {
                VPN.StartPolling();
            }

            WatchdogTimer.Enabled = true;
            UpdateApplicationState();

            // note: this is synchronized so only 1 can run at a time. we're only calling it here to avoid the
            // initial timer tick duration
            _ = AttemptConnection(source: nameof(Start));
        }

        /// <summary>
        ///     Stops the watchdog and aborts any active reconnection loop, then starts again.
        /// </summary>
        /// <remarks>
        ///     This should be called when we have a reason to restart an in-process retry loop, such as a setting change,
        ///     user request, etc.
        /// </remarks>
        public virtual void Restart()
        {
            if (IsEnabled)
            {
                Log.Information("(Re)connection process restarted");
                Stop(abortReconnect: true);
                Start();
            }
        }

        /// <summary>
        ///     Stops monitoring the server connection.
        /// </summary>
        /// <param name="abortReconnect">A value indicating whether to abort an ongoing reconnect attempt.</param>
        /// <remarks>This should be called when the application is reasonably certain that the connection is connected.</remarks>
        public virtual void Stop(bool abortReconnect = false)
        {
            // stop the timer first to avoid having it start the connection process again when the cts is cancelled
            WatchdogTimer.Enabled = false;
            UpdateApplicationState();

            // note: the connect event fires before the connection logic is complete, so there is a chain of events
            // that leads to the application being connected and then immediately disconnected if this CTS is cancelled
            // when the watchdog is stopped due to sucessful connection. pass the cancelReconnect flag only when the user
            // wants to abort the reconnect logic.
            if (abortReconnect)
            {
                try
                {
                    CancellationTokenSource?.Cancel(throwOnFirstException: false);
                }
                catch (Exception)
                {
                    // noop. Cancel() can throw if registered callbacks throw, but we don't have any right now and we don't care anyway
                }
            }
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            Stop(abortReconnect: true);

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

        private void UpdateApplicationState(DateTime? nextAttemptAt = null)
        {
            lock (StateLock)
            {
                ApplicationState.SetValue(state => state with
                {
                    ConnectionWatchdog = new ServerConnectionWatchdogState()
                    {
                        IsEnabled = IsEnabled,
                        IsAttemptingConnection = IsAttemptingConnection,
                        IsAwaitingVpn = IsAwaitingVpn,
                        NextAttemptAt = IsAttemptingConnection ? nextAttemptAt ?? state.ConnectionWatchdog.NextAttemptAt : null, // only null this if we're not attempting, otherwise it sticks
                    },
                });
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

        private async Task AttemptConnection(string source)
        {
            // semaphore is obtained here and released only when the reconnect attempt is complete, so
            // one and only one invocation of this can run at a time
            if (await SyncRoot.WaitAsync(0))
            {
                try
                {
                    /*
                        go until we connect and break, or something stops the watchdog. why don't we just use the timer here?
                        good question. we could, but a loop gives us more control and precision.  at the expensive of needing to
                        break out of it if a user wants to stop or reset.  reevaluate later!

                        things that can cause us to exit this loop:
                        - the timer being disabled, which will exit whenever the while expression is evaluated. any Task.Delay or connection attempt will keep going (this is more of a backstop)
                        - CancellationTokenSource being tripped, which will throw TaskCanceledException or OperationCanceledException somewhere
                        - a successful connection OR the server becomes connected another way
                    */
                    int attempts = 0;

                    while (IsEnabled)
                    {
                        // bail out if we're already connected. it's possible that something else outside of the watchdog
                        // connects the client. highly unlikely but that might change if someone inadvertently adds some logic.
                        if (ApplicationState.CurrentValue.Server.IsConnected)
                        {
                            return;
                        }

                        if (OptionsAtStartup.Integration.Vpn.Enabled && !VPN.IsReady)
                        {
                            IsAwaitingVpn = true;
                            return;
                        }

                        IsAwaitingVpn = false;

                        CancellationTokenSource = new CancellationTokenSource();

                        if (attempts > 0)
                        {
                            var (delay, jitter) = Compute.ExponentialBackoffDelay(
                                iteration: attempts,
                                maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

                            var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
                            Log.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before attempting to reconnect");

                            UpdateApplicationState(nextAttemptAt: DateTime.UtcNow.AddMilliseconds(delay + jitter));

                            await Task.Delay(delay + jitter, cancellationToken: CancellationTokenSource?.Token ?? CancellationToken.None);

                            Log.Information("Attempting to connect to the Soulseek server (#{Attempts})...", attempts);
                        }
                        else
                        {
                            Log.Information("Attempting to connect to the Soulseek server...");
                            UpdateApplicationState(nextAttemptAt: DateTime.UtcNow);
                        }

                        try
                        {
                            // reconnect with the latest configuration values we have for username and password, instead of the
                            // options that were captured at startup. if a user has updated these values prior to the disconnect,
                            // the changes will take effect now.
                            var opt = OptionsMonitor.CurrentValue.Soulseek;

                            // note: cancelling the CTS before the connect logic is fully complete (e.g. reacting to the connect event)
                            // will cause the client to connect, then disconnect immediately
                            await Client.ConnectAsync(
                                address: opt.Address,
                                port: opt.Port,
                                username: opt.Username,
                                password: opt.Password,
                                cancellationToken: CancellationTokenSource?.Token ?? CancellationToken.None);

                            break;
                        }
                        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                        {
                            Log.Information("Reconnection attempt cancelled");
                            Log.Debug(ex, "ConnectAsync() threw {Exception}", ex);
                            return;
                        }
                        catch (Exception ex)
                        {
                            attempts++;
                            Log.Error("Failed to reconnect: {Message}", ex.Message);
                        }
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    Log.Information("Reconnection attempt cancelled");
                    Log.Debug(ex, "Reconnect logic threw {Exception}", ex);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Reconnect attempt failed: {Message}", ex.Message);
                }
                finally
                {
                    var cts = CancellationTokenSource;
                    CancellationTokenSource = null;

                    try
                    {
                        cts?.Dispose();
                    }
                    catch (Exception)
                    {
                        // noop. i don't think this can throw, but if we fail to release the SyncRoot we'll be in trouble.
                    }

                    SyncRoot.Release();

                    // do this after the semaphore is released so that IsAttemptingConnection is false and the next attempt is nulled
                    UpdateApplicationState();
                }
            }
        }
    }
}
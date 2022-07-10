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
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    public interface IConnectionWatchdog : IDisposable
    {
    }

    public class ConnectionWatchdog : IConnectionWatchdog
    {
        private static readonly int ReconnectMaxDelayMilliseconds = 300000; // 5 minutes

        public ConnectionWatchdog(
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<State> state)
        {
            Client = soulseekClient;
            Options = optionsMonitor;
            State = state;

            WatchDogTimer = new System.Timers.Timer()
            {
                Interval = 250,
                AutoReset = true,
                Enabled = true,
            };

            WatchDogTimer.Elapsed += (sender, args) => _ = AttemptReconnect();
        }

        private int Attempts { get; set; }
        private ISoulseekClient Client { get; }
        private bool Disposed { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> Options { get; set; }
        private IStateMonitor<State> State { get; }
        private System.Timers.Timer WatchDogTimer { get; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    WatchDogTimer?.Dispose();
                }

                Disposed = true;
            }
        }

        private async Task AttemptReconnect()
        {
            if (State.CurrentValue.Server.IsConnected || !State.CurrentValue.Server.AttemptingReconnect)
            {
                return;
            }

            var (delay, jitter) = Compute.ExponentialBackoffDelay(
                iteration: Attempts,
                maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

            var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
            Log.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before reconnecting");

            await Task.Delay(delay + jitter);

            Log.Information("Attempting to reconnect (#{Attempts})...", Attempts);

            try
            {
                // reconnect with the latest configuration values we have for username and password, instead of the options that
                // were captured at startup. if a user has updated these values prior to the disconnect, the changes will take
                // effect now.
                await Client.ConnectAsync(Options.CurrentValue.Soulseek.Username, Options.CurrentValue.Soulseek.Password);
            }
            catch (Exception ex)
            {
                Attempts++;
                Log.Error("Failed to reconnect: {Message}", ex.Message);
            }
        }
    }
}
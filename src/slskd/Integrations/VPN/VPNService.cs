// <copyright file="VPNService.cs" company="slskd Team">
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

namespace slskd.Integrations.VPN;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using slskd.Events;
using Soulseek;
using static slskd.Options.IntegrationOptions;

public class VPNService : IDisposable
{
    public VPNService(
        IOptionsMonitor<Options> optionsMonitor,
        EventBus eventBus,
        IHttpClientFactory httpClientFactory)
    {
        OptionsMonitor = optionsMonitor;
        HttpClientFactory = httpClientFactory;

        Timer = new System.Timers.Timer(interval: 1000)
        {
            AutoReset = true,
        };

        Timer.Elapsed += async (_, _) => await CheckConnection();

        EventBus = eventBus;
        EventBus.Subscribe<SoulseekClientDisconnectedEvent>(nameof(VPNService), async e =>
        {
            await SyncRoot.WaitAsync();

            try
            {
                var cts = ConnectedTaskCompletionSource;
                ConnectedTaskCompletionSource = new();
                cts.TrySetException(new SoulseekClientException("Client disconnected"));

                Timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            }
            finally
            {
                SyncRoot.Release();
            }
        });

        if (Options.Enabled)
        {
            if (!string.IsNullOrEmpty(Options.Gluetun.Url))
            {
                Client = new GluetunClient(HttpClientFactory, OptionsMonitor);
                Timer.Start();
                return;
            }

            Log.Error("VPN integration is enabled, but no VPN client has been configured");
        }
    }

    private EventBus EventBus { get; }
    private IVPNClient Client { get; set; }
    private System.Timers.Timer Timer { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<VPNService>();
    private IHttpClientFactory HttpClientFactory { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private VpnOptions Options => OptionsMonitor.CurrentValue.Integration.Vpn;
    private bool Disposed { get; set; }
    private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
    private SemaphoreSlim TimerElapsedLock { get; } = new SemaphoreSlim(1, 1);
    private TaskCompletionSource ConnectedTaskCompletionSource { get; set; } = new();

    public async Task WaitForConnectionAsync()
    {
        await SyncRoot.WaitAsync();

        Task task = default;

        try
        {
            task = ConnectedTaskCompletionSource.Task;
        }
        finally
        {
            SyncRoot.Release();
        }

        _ = CheckConnection();
        await task;
    }

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
                Timer.Dispose();
            }

            Disposed = true;
        }
    }

    private async Task CheckConnection()
    {
        // only one connection process at a time
        await TimerElapsedLock.WaitAsync(0);

        try
        {
            var isConnected = await Client.GetConnectionStatusAsync();

            if (isConnected)
            {
                if (Options.PortForwarding)
                {
                    var port = await Client.GetForwardedPortAsync();

                    Program.ApplyConfigurationOverlay(Program.ConfigurationOverlay with
                    {
                        Soulseek = Program.ConfigurationOverlay.Soulseek with
                        {
                            ListenPort = port,
                        },
                    });
                }

                ConnectedTaskCompletionSource.TrySetResult();

                Timer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to communicate with VPN client: {Message}", ex.Message);
        }
        finally
        {
            TimerElapsedLock.Release();
        }
    }
}
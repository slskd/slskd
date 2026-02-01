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
using Serilog;
using slskd.Events;
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

        Timer.Elapsed += Timer_Elapsed;

        EventBus = eventBus;
        EventBus.Subscribe<SoulseekClientDisconnectedEvent>(nameof(VPNService), e =>
        {
            Timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
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

    private async void Timer_Elapsed(object sender, EventArgs args)
    {
        try
        {
            var isConnected = await Client.GetConnectionStatusAsync();

            if (isConnected)
            {
                var port = await Client.GetForwardedPortAsync();

                Program.ApplyConfigurationOverlay(Program.ConfigurationOverlay with
                {
                    Soulseek = Program.ConfigurationOverlay.Soulseek with
                    {
                        ListenPort = 123,
                    },
                });

                Timer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to communicate with VPN client: {Message}", ex.Message);
        }
    }
}
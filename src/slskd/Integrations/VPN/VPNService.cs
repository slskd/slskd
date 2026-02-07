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
using Soulseek;

public class VPNService : IDisposable
{
    public VPNService(
        OptionsAtStartup optionsAtStartup,
        IOptionsMonitor<Options> optionsMonitor,
        IStateMutator<State> stateMutator,
        ISoulseekClient soulseekClient,
        IHttpClientFactory httpClientFactory)
    {
        OptionsAtStartup = optionsAtStartup;
        OptionsMonitor = optionsMonitor;
        StateMutator = stateMutator;
        SoulseekClient = soulseekClient;
        HttpClientFactory = httpClientFactory;

        if (OptionsAtStartup.Integration.Vpn.Enabled)
        {
            Timer = new System.Timers.Timer(interval: 2500)
            {
                AutoReset = true,
            };

            Timer.Elapsed += async (_, _) => await CheckConnection();

            // detect VPN based on the configuration provided; this logic will determine the order
            // we can revisit this if/when we add other options besides gluetun
            if (!string.IsNullOrEmpty(OptionsMonitor.CurrentValue.Integration.Vpn.Gluetun.Url))
            {
                Client = new GluetunClient(HttpClientFactory, OptionsMonitor);
                Timer.Start();

                Log.Information("VPN integration enabled using {Client}", typeof(GluetunClient).Name);
                return;
            }

            Log.Error("VPN integration is enabled, but no VPN client has been configured");
        }
    }

    public bool IsReady { get; private set; } = false;
    public VPNStatus Status { get; private set; } = new VPNStatus();

    private ISoulseekClient SoulseekClient { get; }
    private IVPNClient Client { get; set; }
    private System.Timers.Timer Timer { get; }
    private IStateMutator<State> StateMutator { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<VPNService>();
    private IHttpClientFactory HttpClientFactory { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private OptionsAtStartup OptionsAtStartup { get; }
    private bool Disposed { get; set; }
    private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
    private SemaphoreSlim TimerElapsedLock { get; } = new SemaphoreSlim(1, 1);

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
        bool isReadyNow = false;
        VPNStatus status = default;

        // one at a time!
        await TimerElapsedLock.WaitAsync(0);

        try
        {
            /*
                step one: fetch status from the VPN client

                this step performs I/O with the VPN client, so do it before obtaining the SyncRoot to avoid
                unnecessary lock contention
            */
            Log.Verbose("Checking VPN status using {Client}", Client.GetType());

            status = await Client.GetStatusAsync();

            // synchronize access to the TaskCompletionSource so we don't swap it out while something is trying to access it
            await SyncRoot.WaitAsync();

            try
            {
                /*
                    step two:
                    * update VPNService state
                    * apply a configuration overlay to set the forwarded port (if there is one)
                    * update application state (via StateMutator)
                    * if IsReady changed during this invocation, disposition the ReadyTaskCompletionSource
                */
                if (!status.IsConnected)
                {
                    isReadyNow = false;
                }
                else
                {
                    if (OptionsMonitor.CurrentValue.Integration.Vpn.PortForwarding)
                    {
                        if (status.ForwardedPort.HasValue)
                        {
                            isReadyNow = true;

                            var overlay = Program.ConfigurationOverlay ?? new OptionsOverlay();
                            overlay = overlay with { Soulseek = overlay.Soulseek ?? new OptionsOverlay.SoulseekOptionsPatch() };

                            // avoid incessant updates
                            if (overlay.Soulseek.ListenPort != status.ForwardedPort)
                            {
                                Program.ApplyConfigurationOverlay(overlay with
                                {
                                    Soulseek = overlay.Soulseek with
                                    {
                                        ListenPort = status.ForwardedPort,
                                    },
                                });
                            }
                        }
                    }
                    else
                    {
                        isReadyNow = true;
                    }
                }

                Status = status;

                // if we weren't ready before and now we are, signal to anyone waiting that we're good to go
                if (!IsReady && isReadyNow)
                {
                    // complete the TCS so anything waiting for ready can progress.  don't replace it; VPN should be assumed
                    // to stay ready until we check and find that it is no longer ready
                    if (status.ForwardedPort.HasValue)
                    {
                        Log.Information("VPN client connected and ready! IP: {PublicIP} ({Location}); forwarding port {Port}", status.PublicIPAddress, status.Location, status.ForwardedPort);
                    }
                    else
                    {
                        Log.Information("VPN client connected and ready! IP: {PublicIP} ({Location})", status.PublicIPAddress, status.Location);
                    }

                    IsReady = isReadyNow;
                }

                // if we were previously ready and now we aren't, the VPN has disconnected or stopped providing a port
                if (IsReady && !isReadyNow)
                {
                    IsReady = isReadyNow;
                    Log.Warning("VPN client disconnected");

                    if (OptionsAtStartup.Integration.Vpn.Required)
                    {
                        SoulseekClient.Disconnect("VPN client disconnected", new VPNClientException("VPN client disconnected"));
                    }
                }
            }
            finally
            {
                StateMutator.SetValue(state => state with
                {
                    Vpn = new VpnState()
                    {
                        IsReady = IsReady,
                        IsConnected = status.IsConnected,
                        PublicIPAddress = status.PublicIPAddress,
                        Location = status.Location,
                        ForwardedPort = status.ForwardedPort,
                    },
                });

                SyncRoot.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to communicate with VPN client: {Message}", ex.Message);
        }
        finally
        {
            TimerElapsedLock.Release();
        }
    }
}
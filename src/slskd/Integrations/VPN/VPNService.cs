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

public class VPNService : IDisposable
{
    public VPNService(
        OptionsAtStartup optionsAtStartup,
        IOptionsMonitor<Options> optionsMonitor,
        EventBus eventBus,
        IStateMonitor<State> stateMonitor,
        IStateMutator<State> stateMutator,
        IHttpClientFactory httpClientFactory)
    {
        OptionsAtStartup = optionsAtStartup;
        OptionsMonitor = optionsMonitor;
        EventBus = eventBus;
        StateMutator = stateMutator;
        StateMonitor = stateMonitor;
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
                return;
            }

            Log.Error("VPN integration is enabled, but no VPN client has been configured");
        }
    }

    public bool IsConnected { get; private set; }
    public int? ForwardedPort { get; private set; }
    public bool IsReady { get; private set; }

    private IVPNClient Client { get; set; }
    private System.Timers.Timer Timer { get; }
    private EventBus EventBus { get; }
    private IStateMutator<State> StateMutator { get; }
    private IStateMonitor<State> StateMonitor { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<VPNService>();
    private IHttpClientFactory HttpClientFactory { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private OptionsAtStartup OptionsAtStartup { get; }
    private bool Disposed { get; set; }
    private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
    private SemaphoreSlim TimerElapsedLock { get; } = new SemaphoreSlim(1, 1);
    private TaskCompletionSource ReadyTaskCompletionSource { get; set; } = new();

    public async Task WaitForReadyAsync()
    {
        await SyncRoot.WaitAsync();

        Task task = default;

        try
        {
            task = ReadyTaskCompletionSource.Task;
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
        var initialIsReady = IsReady;
        bool isConnected = false;
        int? port = null;

        // one at a time!
        await TimerElapsedLock.WaitAsync(0);

        try
        {
            /*
                step one: fetch status from the VPN client

                this step performs I/O with the VPN client, so do it before obtaining the SyncRoot to avoid
                unnecessary lock contention
            */
            Log.Debug("Checking VPN status using {Client}", Client.GetType());

            isConnected = await Client.GetIsConnectedAsync();

            if (isConnected && OptionsMonitor.CurrentValue.Integration.Vpn.PortForwarding)
            {
                Log.Debug("Fetching forwarded port from {Client}", Client.GetType());

                port = await Client.GetForwardedPortAsync();

                if (port.HasValue && (port < 1024 || port > 65535))
                {
                    Log.Warning("VPN client provided a forwarded port outside of the supported range of 1024-65535 (given: {Port})", ForwardedPort);
                    port = null;
                }
            }

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
                if (!isConnected)
                {
                    IsConnected = false;
                    IsReady = false;
                    ForwardedPort = null;
                }
                else
                {
                    IsConnected = true;

                    if (OptionsMonitor.CurrentValue.Integration.Vpn.PortForwarding)
                    {
                        if (port.HasValue)
                        {
                            ForwardedPort = port;
                            IsReady = true;

                            // always apply the provided port, as it may change while the app is running
                            Program.ApplyConfigurationOverlay(Program.ConfigurationOverlay with
                            {
                                Soulseek = Program.ConfigurationOverlay.Soulseek with
                                {
                                    ListenPort = ForwardedPort,
                                },
                            });
                        }
                    }
                    else
                    {
                        IsReady = true;
                    }
                }

                // if we weren't ready before and now we are, signal to anyone waiting that we're good to go
                if (!initialIsReady && IsReady)
                {
                    // complete the TCS so anything waiting for ready can progress.  don't replace it; VPN should be assumed
                    // to stay ready until we check and find that it is no longer ready
                    ReadyTaskCompletionSource.SetResult();
                }

                // if we were previously ready and now we aren't, the VPN has disconnected or stopped providing a port
                if (initialIsReady && !IsReady)
                {
                    var tcs = ReadyTaskCompletionSource;

                    // replace the TCS with a new one; anything that waits on it needs to wait for a new transition to ready
                    ReadyTaskCompletionSource = new TaskCompletionSource();

                    // throw the existing TCS so anything waiting for it dies (otherwise risk of waiting forever)
                    tcs.SetException(new VPNClientException("VPN client is no longer ready"));
                }
            }
            finally
            {
                StateMutator.SetValue(state => state with
                {
                    Vpn = new VpnState()
                    {
                        IsConnected = IsConnected,
                        ForwardedPort = ForwardedPort,
                        IsReady = IsReady,
                    },
                });

                SyncRoot.Release();
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
// <copyright file="GluetunClient.cs" company="slskd Team">
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
using System.Net.Http.Json;
using System.Threading.Tasks;
using Serilog;
using static slskd.Options.IntegrationOptions.VpnOptions;

public enum GluetunClientAuthenticationMethod
{
    None,
    Basic,
    ApiKey,
}

public class GluetunClient : IVPNClient
{
    public GluetunClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<Options> optionsMonitor)
    {
        HttpClientFactory = httpClientFactory;
        OptionsMonitor = optionsMonitor;
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<GluetunClient>();
    private IHttpClientFactory HttpClientFactory { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private GluetunVpnOptions Options => OptionsMonitor.CurrentValue.Integration.Vpn.Gluetun;

    public async Task<bool> GetConnectionStatusAsync()
    {
        using var http = HttpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMilliseconds(1000);
        ConfigureAuth(http);

        try
        {
            using var response = await http.GetAsync($"{Options.Url.TrimEnd('/')}/v1/vpn/status");
            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<GluetunStatusResponse>()
                ?? throw new Exception($"Failed to deserialize Gluetun status response; got: {await response.Content.ReadAsStringAsync()}");

            return status.Status.Equals("running", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to retrieve status from Gluetun: {Message}", ex.Message);
            throw new VPNClientException($"Failed to retrieve status from Gluetun: {ex.Message}", ex);
        }
    }

    public async Task<int> GetForwardedPortAsync()
    {
        using var http = HttpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMilliseconds(1000);
        ConfigureAuth(http);

        try
        {
            using var response = await http.GetAsync($"{Options.Url.TrimEnd('/')}/v1/portforward");
            response.EnsureSuccessStatusCode();

            var port = await response.Content.ReadFromJsonAsync<GluetunPortForwardResponse>()
                ?? throw new Exception($"Failed to deserialize Gluetun port forward response; got: {await response.Content.ReadAsStringAsync()}");

            return port.Port;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to retrieve status from Gluetun: {Message}", ex.Message);
            throw new VPNClientException($"Failed to retrieve status from Gluetun: {ex.Message}", ex);
        }
    }

    private void ConfigureAuth(HttpClient client)
    {
        if (Options.Auth.Equals(GluetunClientAuthenticationMethod.Basic.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var creds = $"{Options.Username}:{Options.Password}".ToBase64();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {creds}");
        }
        else
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", Options.ApiKey);
        }
    }

    private class GluetunStatusResponse
    {
        public string Status { get; init; }
    }

    private class GluetunPortForwardResponse
    {
        public int Port { get; init; }
    }
}
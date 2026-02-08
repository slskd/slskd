// <copyright file="Gluetun.cs" company="slskd Team">
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using static slskd.Options.IntegrationOptions.VpnOptions;

/// <summary>
///     Gluetun VPN client authentication methods.
/// </summary>
public enum GluetunClientAuthenticationMethod
{
    /// <summary>
    ///     The Gluetun control server allows unauthenticated requests.
    /// </summary>
    None,

    /// <summary>
    ///     The Glueton control server is configured to use HTTP Basic authentication; username and password are required.
    /// </summary>
    Basic,

    /// <summary>
    ///     The Gluetun control server is configured to use API key authentication; apiKey is required.
    /// </summary>
    ApiKey,
}

/// <summary>
///     Gluetun VPN client.
/// </summary>
public class Gluetun : IVPNClient
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Gluetun"/> class.
    /// </summary>
    /// <param name="httpClientFactory"></param>
    /// <param name="optionsMonitor"></param>
    public Gluetun(IHttpClientFactory httpClientFactory, IOptionsMonitor<Options> optionsMonitor)
    {
        HttpClientFactory = httpClientFactory;
        OptionsMonitor = optionsMonitor;
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Gluetun>();
    private IHttpClientFactory HttpClientFactory { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private GluetunVpnOptions Options => OptionsMonitor.CurrentValue.Integration.Vpn.Gluetun;

    /// <summary>
    ///     Fetch the VPN connection status from the Gluetun control server.
    /// </summary>
    /// <returns>A value indicating whether the VPN is connected.</returns>
    public async Task<VPNStatus> GetStatusAsync()
    {
        using var http = HttpClientFactory.CreateClient();

        http.Timeout = TimeSpan.FromMilliseconds(Options.Timeout);

        if (Options.Auth.Equals(GluetunClientAuthenticationMethod.Basic.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var creds = $"{Options.Username}:{Options.Password}".ToBase64();
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {creds}");
        }
        else if (Options.Auth.Equals(GluetunClientAuthenticationMethod.ApiKey.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", Options.ApiKey);
        }
        else
        {
            // no auth
        }

        try
        {
            var publicIp = await MakeRequest<GluetunPublicIpResponse>(http, "/v1/publicip/ip");

            // per the gluetun docs/discussions, the public ip field will be an empty string if the VPN isn't up
            if (string.IsNullOrEmpty(publicIp.PublicIp))
            {
                return new VPNStatus { IsConnected = false };
            }

            int? port = null;

            if (OptionsMonitor.CurrentValue.Integration.Vpn.PortForwarding)
            {
                port = (await MakeRequest<GluetunPortForwardResponse>(http, "/v1/portforward"))?.Port;

                // port will be 0 if port forwarding isn't enabled or ready
                if (port == 0)
                {
                    port = null;
                }
            }

            return new VPNStatus
            {
                IsConnected = true,
                PublicIPAddress = IPAddress.Parse(publicIp.PublicIp),
                Location = string.Join(", ", [publicIp.City, publicIp.Country]),
                ForwardedPort = port,
            };
        }
        catch (Exception ex)
        {
            Log.Debug("Failed to retrieve status from Gluetun: {Message}", ex.Message);
            throw new VPNClientException(ex.Message, ex);
        }
    }

    private async Task<T> MakeRequest<T>(HttpClient http, string url)
    {
        using var response = await http.GetAsync($"{Options.Url.TrimEnd('/')}{url}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<T>()
            ?? throw new Exception($"Unexpected Gluetun response; expected: {nameof(T)},  got: {await response.Content.ReadAsStringAsync()}");

        return result;
    }

    private class GluetunStatusResponse
    {
        public string Status { get; init; }
    }

    private class GluetunPortForwardResponse
    {
        public int? Port { get; init; }
    }

    private class GluetunPublicIpResponse
    {
        [JsonPropertyName("public_ip")]
        public string PublicIp { get; init; }
        public string Region { get; init; }
        public string Country { get; init; }
        public string City { get; init; }
        public string Location { get; init; }
        public string Organization { get; init; }

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; init; }
        public string Timezone { get; init; }
    }
}
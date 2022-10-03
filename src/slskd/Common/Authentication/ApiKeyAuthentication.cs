// <copyright file="ApiKeyAuthentication.cs" company="slskd Team">
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

namespace slskd.Authentication
{
    using System;
    using System.Linq;
    using System.Security.Principal;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using NetTools;
    using static slskd.Authentication.ApiKeyAuthenticationHandler;

    /// <summary>
    ///     API key authentication.
    /// </summary>
    public static class ApiKeyAuthentication
    {
        /// <summary>
        ///     Gets the API key authentication scheme name.
        /// </summary>
        public static string AuthenticationScheme { get; } = "API Key";
    }

    /// <summary>
    ///     Handles API key authentication.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="slskdOptionsSnapshot">Slskd options monitor.</param>
        /// <param name="apiKeyOptionsMonitor">An options monitor.</param>
        /// <param name="logger">A logger factory.</param>
        /// <param name="urlEncoder">A url encoder.</param>
        /// <param name="systemClock">A system clock interface.</param>
        public ApiKeyAuthenticationHandler(
            IOptionsSnapshot<Options> slskdOptionsSnapshot,
            IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptionsMonitor,
            ILoggerFactory logger,
            UrlEncoder urlEncoder,
            ISystemClock systemClock)
            : base(apiKeyOptionsMonitor, logger, urlEncoder, systemClock)
        {
            SlskdOptionsSnapshot = slskdOptionsSnapshot;
        }

        private IOptionsSnapshot<Options> SlskdOptionsSnapshot { get; }

        /// <summary>
        ///     Authenticates via API key.
        /// </summary>
        /// <returns>A successful authentication result containing a default ticket.</returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Task.Yield();

            var slskdOptions = SlskdOptionsSnapshot.Value;
            var apiKeyOptions = OptionsMonitor.CurrentValue;

            string key = default;

            if (Request.Headers.TryGetValue("X-API-Key", out var headerKeyValue))
            {
                key = headerKeyValue;
            }
            else if (apiKeyOptions.EnableSignalRSupport && Request.Path.StartsWithSegments(apiKeyOptions.SignalRRoutePrefix) && Request.Query.ContainsKey("access_token"))
            {
                // assign the request token from the access_token query parameter
                // but only if the destination is a SignalR hub
                // https://docs.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-5.0
                key = Request.Query["access_token"];
            }
            else
            {
                return AuthenticateResult.NoResult();
            }

            var matchingRecord = slskdOptions.Web.Authentication.ApiKeys
                .FirstOrDefault(kvp => kvp.Value.Key == key);

            if (matchingRecord.Key == null)
            {
                return AuthenticateResult.Fail(new Exception("The provided API key does not match an existing key"));
            }

            var ip = Request.HttpContext.Connection.RemoteIpAddress;

            if (!matchingRecord.Value.Cidr.Split(',')
                .Select(cidr => IPAddressRange.Parse(cidr))
                .Any(range => range.Contains(ip)))
            {
                return AuthenticateResult.Fail(new Exception("The remote IP address is not within the range specified for the key"));
            }

            var identity = new GenericIdentity(matchingRecord.Key);
            var principal = new GenericPrincipal(identity, new[] { apiKeyOptions.Role.ToString() });
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), ApiKeyAuthentication.AuthenticationScheme);

            return AuthenticateResult.Success(ticket);
        }

        /// <summary>
        ///     API key authentication options.
        /// </summary>
        public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="ApiKeyAuthenticationOptions"/> class.
            /// </summary>
            public ApiKeyAuthenticationOptions()
            {
            }

            /// <summary>
            ///     Gets or sets the route prefix used to identify SignalR authentication attempts.
            /// </summary>
            public string SignalRRoutePrefix { get; set; }

            /// <summary>
            ///     Gets or sets a value indicating whether to support SignalR authentication.
            /// </summary>
            public bool EnableSignalRSupport { get; set; }

            /// <summary>
            ///     Gets or sets the role for authenticated tickets.
            /// </summary>
            public Role Role { get; set; } = Role.Administrator;
        }
    }
}

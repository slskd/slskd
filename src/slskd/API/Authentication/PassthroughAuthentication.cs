// <copyright file="PassthroughAuthentication.cs" company="slskd Team">
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

namespace slskd.API.Authentication
{
    using System.Security.Principal;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Passthrough authentication.
    /// </summary>
    public static class PassthroughAuthentication
    {
        /// <summary>
        ///     Gets the Passthrough authentication scheme name.
        /// </summary>
        public static string AuthenticationScheme { get; } = "Passthrough";
    }

    /// <summary>
    ///     Handles passthrough authentication.
    /// </summary>
    public class PassthroughAuthenticationHandler : AuthenticationHandler<PassthroughAuthenticationOptions>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PassthroughAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="optionsMonitor">An options monitor.</param>
        /// <param name="logger">A logger factory.</param>
        /// <param name="urlEncoder">A url encoder.</param>
        /// <param name="systemClock">A system clock interface.</param>
        public PassthroughAuthenticationHandler(IOptionsMonitor<PassthroughAuthenticationOptions> optionsMonitor, ILoggerFactory logger, UrlEncoder urlEncoder, ISystemClock systemClock)
            : base(optionsMonitor, logger, urlEncoder, systemClock)
        {
        }

        /// <summary>
        ///     Authenticates using the configured <see cref="PassthroughAuthenticationOptions.Username"/> and <see cref="PassthroughAuthenticationOptions.Role"/>.
        /// </summary>
        /// <returns>A successful authentication result containing a default ticket.</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new GenericIdentity(Options.Username);
            var principal = new GenericPrincipal(identity, new[] { Options.Role.ToString() });
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), PassthroughAuthentication.AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    /// <summary>
    ///     Passthrough authentication options.
    /// </summary>
    public class PassthroughAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PassthroughAuthenticationOptions"/> class.
        /// </summary>
        public PassthroughAuthenticationOptions()
        {
        }

        /// <summary>
        ///     Gets or sets the username for the passed-through authentication ticket.
        /// </summary>
        public string Username { get; set; } = "Anonymous";

        /// <summary>
        ///     Gets or sets the role for the passed-through authentication ticket.
        /// </summary>
        public Role Role { get; set; } = Role.Administrator;
    }
}

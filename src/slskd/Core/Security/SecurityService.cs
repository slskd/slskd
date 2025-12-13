// <copyright file="SecurityService.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Net;
    using System.Security.Claims;
    using Microsoft.IdentityModel.Tokens;
    using NetTools;
    using slskd.Authentication;

    public interface ISecurityService
    {
        JwtSecurityToken GenerateJwt(string username, Role role, int? ttl = null);
        (string Name, Role Role) AuthenticateWithApiKey(string key, IPAddress callerIpAddress);
    }

    public class SecurityService : ISecurityService
    {
        public SecurityService(
            SymmetricSecurityKey jwtSigningKey,
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor)
        {
            JwtSigningKey = jwtSigningKey;
            OptionsAtStartup = optionsAtStartup;
            OptionsMonitor = optionsMonitor;

            var adminApiKeyString = OptionsMonitor.CurrentValue.Web.Authentication.ApiKey;

            if (!string.IsNullOrWhiteSpace(adminApiKeyString))
            {
                if (!adminApiKeyString.Contains(';'))
                {
                    AdministrativeApiKey = new Options.WebOptions.WebAuthenticationOptions.ApiKeyOptions
                    {
                        Key = adminApiKeyString,
                    };
                }

                var tuples = adminApiKeyString.Split(';');

                string key = null;
                string role = null;
                string cidr = null;

                foreach (var tuple in tuples)
                {
                    if (tuple.StartsWith("role=", StringComparison.OrdinalIgnoreCase))
                    {
                        role = tuple.Split('=').LastOrDefault();
                    }
                    else if (tuple.StartsWith("cidr=", StringComparison.OrdinalIgnoreCase))
                    {
                        cidr = tuple.Split('=').LastOrDefault();
                    }
                    else
                    {
                        key = tuple;
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    AdministrativeApiKey = new Options.WebOptions.WebAuthenticationOptions.ApiKeyOptions
                    {
                        Key = key,
                        Role = role,
                        Cidr = cidr,
                    };
                }
            }
        }

        private SymmetricSecurityKey JwtSigningKey { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private Options.WebOptions.WebAuthenticationOptions.ApiKeyOptions AdministrativeApiKey { get; } = null;

        public (string Name, Role Role) AuthenticateWithApiKey(string key, IPAddress callerIpAddress)
        {
            var keys = OptionsMonitor.CurrentValue.Web.Authentication.ApiKeys.AsEnumerable();

            if (AdministrativeApiKey is not null)
            {
                keys = keys.Prepend(new KeyValuePair<string, Options.WebOptions.WebAuthenticationOptions.ApiKeyOptions>(nameof(AdministrativeApiKey), AdministrativeApiKey));
            }

            var record = keys.FirstOrDefault(k => k.Value.Key == key);

            if (record.Key == null)
            {
                throw new NotFoundException($"The provided API key does not match an existing key");
            }

            if (!record.Value.Cidr.Split(',')
                .Select(cidr => IPAddressRange.Parse(cidr))
                .Any(range => range.Contains(callerIpAddress)))
            {
                throw new OutOfRangeException("The remote IP address is not within the range specified for the key");
            }

            return (record.Key, record.Value.Role.ToEnum<Role>());
        }

        public JwtSecurityToken GenerateJwt(string username, Role role, int? ttl = null)
        {
            var issuedUtc = DateTime.UtcNow;
            var expiresUtc = DateTime.UtcNow.AddMilliseconds(ttl ?? OptionsAtStartup.Web.Authentication.Jwt.Ttl);

            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("name", username),
                new Claim("iat", ((DateTimeOffset)issuedUtc).ToUnixTimeSeconds().ToString()),
            };

            var credentials = new SigningCredentials(JwtSigningKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Program.AppName,
                claims: claims,
                notBefore: issuedUtc,
                expires: expiresUtc,
                signingCredentials: credentials);

            return token;
        }
    }
}

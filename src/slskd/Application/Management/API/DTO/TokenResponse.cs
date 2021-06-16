// <copyright file="TokenResponse.cs" company="slskd Team">
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

namespace slskd.Management.API
{
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using System.Text.Json.Serialization;
    using Microsoft.AspNetCore.Authentication.JwtBearer;

    public class TokenResponse
    {
        public TokenResponse(JwtSecurityToken jwtSecurityToken)
        {
            JwtSecurityToken = jwtSecurityToken;
        }

        /// <summary>
        ///     Gets the time at which the Access Token expires.
        /// </summary>
        public long Expires => ((DateTimeOffset)JwtSecurityToken.ValidTo).ToUnixTimeSeconds();

        /// <summary>
        ///     Gets the time at which the Access Token was issued.
        /// </summary>
        public long Issued => ((DateTimeOffset)JwtSecurityToken.ValidFrom).ToUnixTimeSeconds();

        /// <summary>
        ///     Gets the value of the Name claim from the Access Token.
        /// </summary>
        public string Name => JwtSecurityToken.Claims.SingleOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        /// <summary>
        ///     Gets the value of the Not Before claim from the Access Token.
        /// </summary>
        public long NotBefore => long.Parse(JwtSecurityToken.Claims.SingleOrDefault(c => c.Type == "nbf").Value);

        /// <summary>
        ///     Gets the Access Token string.
        /// </summary>
        public string Token => new JwtSecurityTokenHandler().WriteToken(JwtSecurityToken);

        /// <summary>
        ///     Gets the Token type.
        /// </summary>
        public string TokenType => JwtBearerDefaults.AuthenticationScheme;

        [JsonIgnore]
        private JwtSecurityToken JwtSecurityToken { get; }
    }
}
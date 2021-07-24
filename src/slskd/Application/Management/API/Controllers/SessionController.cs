// <copyright file="SessionController.cs" company="slskd Team">
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

namespace slskd.Management.API
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.IdentityModel.Tokens;
    using slskd.Authentication;

    /// <summary>
    ///     Session.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class SessionController : ControllerBase
    {
        public SessionController(
            IOptionsSnapshot<Options> optionsSnapshot,
            OptionsAtStartup optionsAtStartup,
            SymmetricSecurityKey jwtSigningKey)
        {
            OptionsSnapshot = optionsSnapshot;
            OptionsAtStartup = optionsAtStartup;
            JwtSigningKey = jwtSigningKey;
        }

        private SymmetricSecurityKey JwtSigningKey { get; set; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; set; }
        private OptionsAtStartup OptionsAtStartup { get; set; }

        /// <summary>
        ///     Checks whether the provided authentication is valid.
        /// </summary>
        /// <remarks>This is a no-op provided so that the application can test for an expired token on load.</remarks>
        /// <returns></returns>
        /// <response code="200">The authentication is valid.</response>
        /// <response code="403">The authentication is is invalid.</response>
        [HttpGet]
        [Route("")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public IActionResult Check()
        {
            return Ok();
        }

        /// <summary>
        ///     Checks whether security is enabled.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">True if security is enabled, false otherwise.</response>
        [HttpGet]
        [Route("enabled")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(bool), 200)]
        public IActionResult Enabled()
        {
            return Ok(!OptionsAtStartup.Web.Authentication.Disable);
        }

        /// <summary>
        ///     Logs in.
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        /// <response code="200">Login was successful.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="401">Login failed.</response>
        [HttpPost]
        [Route("")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TokenResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public IActionResult Login([FromBody] LoginRequest login)
        {
            if (login == default)
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                return BadRequest("Username and/or Password missing or invalid");
            }

            // only admin login for now
            if (OptionsSnapshot.Value.Web.Authentication.Username == login.Username && OptionsSnapshot.Value.Web.Authentication.Password == login.Password)
            {
                return Ok(new TokenResponse(GetJwtSecurityToken(login.Username, Role.Administrator)));
            }

            return Unauthorized();
        }

        private JwtSecurityToken GetJwtSecurityToken(string username, Role role)
        {
            var issuedUtc = DateTime.UtcNow;
            var expiresUtc = DateTime.UtcNow.AddMilliseconds(OptionsAtStartup.Web.Authentication.Jwt.Ttl);

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
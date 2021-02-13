namespace slskd.API.DTO
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

        [JsonIgnore]
        private JwtSecurityToken JwtSecurityToken { get; }

        /// <summary>
        ///     Gets the Access Token string.
        /// </summary>
        public string Token => new JwtSecurityTokenHandler().WriteToken(JwtSecurityToken);

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
        public string Name => JwtSecurityToken.Claims.Where(c => c.Type == ClaimTypes.Name).SingleOrDefault().Value;

        /// <summary>
        ///     Gets the value of the Not Before claim from the Access Token.
        /// </summary>
        public long NotBefore => long.Parse(JwtSecurityToken.Claims.Where(c => c.Type == "nbf").SingleOrDefault().Value);

        /// <summary>
        ///     Gets the Token type.
        /// </summary>
        public string TokenType => JwtBearerDefaults.AuthenticationScheme;
    }
}
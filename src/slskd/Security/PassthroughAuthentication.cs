namespace slskd.Security
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Security.Principal;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;

    public class PassthroughAuthentication
    {
        public static string AuthenticationScheme { get; } = "Passthrough";
    }

    public class PassthroughAuthenticationHandler : AuthenticationHandler<PassthroughAuthenticationOptions>
    {
        public PassthroughAuthenticationHandler(IOptionsMonitor<PassthroughAuthenticationOptions> optionsMonitor, ILoggerFactory logger, UrlEncoder urlEncoder, ISystemClock systemClock)
            : base(optionsMonitor, logger, urlEncoder, systemClock)
        {            
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new GenericIdentity(Options.Username);
            var principal = new GenericPrincipal(identity, new[] { "User" });
            var ticket = new AuthenticationTicket(principal , new AuthenticationProperties(), PassthroughAuthentication.AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class PassthroughAuthenticationOptions : AuthenticationSchemeOptions
    {
        public PassthroughAuthenticationOptions()
        {
        }

        public string Username { get; set; }
    }
}

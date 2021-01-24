namespace slskd.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Diagnostics;
    using System;
    using Microsoft.Extensions.Hosting;
    using Soulseek;

    /// <summary>
    ///     Application
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ApplicationController : ControllerBase
    {
        private ISoulseekClient Client { get; }
        private IHostApplicationLifetime Lifetime { get; }

        public ApplicationController(ISoulseekClient client, IHostApplicationLifetime lifetime)
        {
            Client = client;
            Lifetime = lifetime;
        }

        /// <summary>
        ///     Stops the application.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Authorize]
        public IActionResult Shutdown()
        {
            Client.Disconnect("Shut down via API");

            Lifetime.StopApplication();
            return NoContent();
        }

        /// <summary>
        ///     Restarts the application.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Authorize]
        public IActionResult Restart()
        {
            Client.Disconnect("Restarted via API");

            Process.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.CommandLine);
            Lifetime.StopApplication();

            return NoContent();
        }
    }
}

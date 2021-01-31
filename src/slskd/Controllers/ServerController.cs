namespace slskd.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;
    using System.Threading.Tasks;
    using slskd.DTO;

    /// <summary>
    ///     Server
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ServerController : ControllerBase
    {
        private ISoulseekClient Client { get; }

        public ServerController(ISoulseekClient client)
        {
            Client = client;
        }

        /// <summary>
        ///     Disconnects the client.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [HttpDelete]
        [Authorize]
        public IActionResult Disconnect([FromBody]string message)
        {
            Client.Disconnect(message);
            return NoContent();
        }

        /// <summary>
        ///     Connects the client.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Connect([FromBody]ConnectRequest req)
        {
            var addr = !string.IsNullOrEmpty(req.Address);
            var port = req.Port.HasValue;
            var un = !string.IsNullOrEmpty(req.Username);
            var pw = !string.IsNullOrEmpty(req.Password);

            if (addr && port && un && pw)
            {
                await Client.ConnectAsync(req.Address, req.Port.Value, req.Username, req.Password);
                return Ok();
            }

            if (!addr && !port && un && pw)
            {
                await Client.ConnectAsync(req.Username, req.Password);
                return Ok();
            }

            return BadRequest("Provide one of the following: address and port, username and password, or address, port, username and password");
        }
    }
}

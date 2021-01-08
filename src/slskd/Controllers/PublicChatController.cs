namespace slskd.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;
    using System.Threading.Tasks;

    /// <summary>
    ///     Server
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class PublicChatController : ControllerBase
    {
        private ISoulseekClient Client { get; }

        public PublicChatController(ISoulseekClient client)
        {
            Client = client;
        }

        /// <summary>
        ///     Starts public chat.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Start()
        {
            await Client.StartPublicChatAsync();
            return StatusCode(StatusCodes.Status201Created);
        }

        /// <summary>
        ///     Stops public chat.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> Stop()
        {
            await Client.StopPublicChatAsync();
            return StatusCode(StatusCodes.Status204NoContent);
        }
    }
}

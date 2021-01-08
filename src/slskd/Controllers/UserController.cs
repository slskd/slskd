namespace slskd.Controllers
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;
    using slskd.DTO;
    using slskd.Trackers;

    /// <summary>
    ///     Users
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class UserController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserController"/> class.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="browseTracker"></param>
        public UserController(ISoulseekClient client, IBrowseTracker browseTracker)
        {
            Client = client;
            BrowseTracker = browseTracker;
        }

        private ISoulseekClient Client { get; }
        private IBrowseTracker BrowseTracker { get; }

        /// <summary>
        ///     Retrieves the address of the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("{username}/address")]
        [Authorize]
        [ProducesResponseType(typeof(UserAddress), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Address([FromRoute, Required]string username)
        {
            try
            {
                var endpoint = await Client.GetUserEndPointAsync(username);
                return Ok(new UserAddress() { IPAddress = endpoint.Address.ToString(), Port = endpoint.Port });
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        ///     Retrieves the files shared by the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/browse")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<Directory>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Browse([FromRoute, Required]string username)
        {
            try
            {
                var result = await Client.BrowseAsync(username);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    BrowseTracker.TryRemove(username);
                });

                return Ok(result);
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        ///     Retrieves the status of the current browse operation for the specified <paramref name="username"/>, if any.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/browse/status")]
        [Authorize]
        [ProducesResponseType(typeof(decimal), 200)]
        [ProducesResponseType(404)]
        public IActionResult BrowseStatus([FromRoute, Required]string username)
        {
            if (BrowseTracker.TryGet(username, out var progress))
            {
                return Ok(progress);
            }

            return NotFound();
        }

        /// <summary>
        ///     Retrieves information about the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/info")]
        [Authorize]
        [ProducesResponseType(typeof(UserInfo), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Info([FromRoute, Required]string username)
        {
            try
            {
                var response = await Client.GetUserInfoAsync(username);
                return Ok(response);
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        ///     Retrieves status for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/status")]
        [Authorize]
        [ProducesResponseType(typeof(UserStatus), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Status([FromRoute, Required]string username)
        {
            try
            {
                var response = await Client.GetUserStatusAsync(username);
                return Ok(response);
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
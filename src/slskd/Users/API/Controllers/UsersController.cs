// <copyright file="UsersController.cs" company="slskd Team">
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

namespace slskd.Users.API
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;

    /// <summary>
    ///     Users.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class UsersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="browseTracker"></param>
        /// <param name="peerService"></param>
        public UsersController(ISoulseekClient soulseekClient, IBrowseTracker browseTracker, IUserService peerService)
        {
            Client = soulseekClient;
            BrowseTracker = browseTracker;
            Peers = peerService;
        }

        private IBrowseTracker BrowseTracker { get; }
        private ISoulseekClient Client { get; }
        private IUserService Peers { get; }

        /// <summary>
        ///     Retrieves the address of the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("{username}/endpoint")]
        [Authorize]
        [ProducesResponseType(typeof(IPEndPoint), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Endpoint([FromRoute, Required] string username)
        {
            try
            {
                var endpoint = await Peers.GetIPEndPointAsync(username);
                return Ok(endpoint);
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
        public async Task<IActionResult> Browse([FromRoute, Required] string username)
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
        public IActionResult BrowseStatus([FromRoute, Required] string username)
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
        /// <param name="bypassCache">Bypasses the internal cache and requests the latest information from the peer.</param>
        /// <returns></returns>
        [HttpGet("{username}/info")]
        [Authorize]
        [ProducesResponseType(typeof(Info), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Info([FromRoute, Required] string username, [FromQuery]bool bypassCache = false)
        {
            try
            {
                var response = await Peers.GetInfoAsync(username, bypassCache);
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
        [ProducesResponseType(typeof(Status), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Status([FromRoute, Required] string username)
        {
            try
            {
                var response = await Peers.GetStatusAsync(username);
                return Ok(response);
            }
            catch (UserOfflineException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
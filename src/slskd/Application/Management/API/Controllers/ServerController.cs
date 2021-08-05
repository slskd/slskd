// <copyright file="ServerController.cs" company="slskd Team">
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
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Server.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ServerController : ControllerBase
    {
        public ServerController(
            IManagementService managementService)
        {
            Management = managementService;
        }

        private IManagementService Management { get; }

        /// <summary>
        ///     Connects the client.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> Connect()
        {
            if (!Management.ServerState.IsConnected)
            {
                await Management.ConnectServerAsync();
            }

            return Ok();
        }

        /// <summary>
        ///     Disconnects the client.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        public IActionResult Disconnect([FromBody] string message)
        {
            if (Management.ServerState.IsConnected)
            {
                Management.DisconnectServer(message);
            }

            return NoContent();
        }

        /// <summary>
        ///     Retrieves the current state of the server.
        /// </summary>
        /// <returns></returns>
        /// <response code="200"></response>
        [HttpGet]
        [Route("")]
        [Authorize]
        [ProducesResponseType(typeof(ServerState), 200)]
        [ProducesResponseType(403)]
        public IActionResult Get()
        {
            return Ok(Management.ServerState);
        }
    }
}
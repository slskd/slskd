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

namespace slskd.API.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.API.DTO;
    using Soulseek;

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
        public ServerController(ISoulseekClient client)
        {
            Client = client;
        }

        private ISoulseekClient Client { get; }

        /// <summary>
        ///     Connects the client.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Connect([FromBody] ConnectRequest req)
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

        /// <summary>
        ///     Disconnects the client.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [HttpDelete]
        [Authorize]
        public IActionResult Disconnect([FromBody] string message)
        {
            Client.Disconnect(message);
            return NoContent();
        }
    }
}
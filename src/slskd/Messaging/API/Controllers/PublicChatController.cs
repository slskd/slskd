// <copyright file="PublicChatController.cs" company="slskd Team">
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

namespace slskd.Messaging.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Relay;
    using Soulseek;

    /// <summary>
    ///     Server.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class PublicChatController : ControllerBase
    {
        public PublicChatController(ISoulseekClient soulseekClient, IOptionsSnapshot<Options> optionsSnapshot)
        {
            Client = soulseekClient;
            OptionsSnapshot = optionsSnapshot;
        }

        private ISoulseekClient Client { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private bool IsAgent => OptionsSnapshot.Value.Relay.Mode.ToEnum<OperationMode>() == OperationMode.Agent;

        /// <summary>
        ///     Starts public chat.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Start()
        {
            if (IsAgent)
            {
                return Forbid();
            }

            await Client.StartPublicChatAsync();
            return StatusCode(StatusCodes.Status201Created);
        }

        /// <summary>
        ///     Stops public chat.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Stop()
        {
            if (IsAgent)
            {
                return Forbid();
            }

            await Client.StopPublicChatAsync();
            return StatusCode(StatusCodes.Status204NoContent);
        }
    }
}

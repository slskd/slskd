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

using Microsoft.Extensions.Options;

namespace slskd.Core.API
{
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
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
        public ServerController(
            ISoulseekClient soulseekClient,
            ConnectionWatchdog connectionWatchdog,
            IOptionsSnapshot<Options> optionsSnapshot,
            IStateSnapshot<State> stateSnapshot)
        {
            Client = soulseekClient;
            ConnectionWatchdog = connectionWatchdog;
            OptionsSnapshot = optionsSnapshot;
            StateSnapshot = stateSnapshot;
        }

        private ISoulseekClient Client { get; }
        private ConnectionWatchdog ConnectionWatchdog { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private IStateSnapshot<State> StateSnapshot { get; }

        /// <summary>
        ///     Connects the client.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(StatusCodes.Status205ResetContent)]
        [ProducesResponseType(403)]
        public IActionResult Connect()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            // if the watchdog is already enabled, we're already attempting to connect. the user might have changed something
            // or perhaps is just being impatient; either way, restart the retry loop so they can see results faster
            // remember, we may be in a ~5 minute delay at this moment
            if (ConnectionWatchdog.IsEnabled)
            {
                ConnectionWatchdog.Restart();
                return StatusCode(StatusCodes.Status205ResetContent);
            }

            if (!ConnectionWatchdog.IsEnabled)
            {
                ConnectionWatchdog.Start();
                return Ok();
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
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        public IActionResult Disconnect([FromBody] string message)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            // stop the watchdog so that it will exit any retry logic
            ConnectionWatchdog.Stop(abortReconnect: true);

            if (Client.State.HasFlag(SoulseekClientStates.Connected))
            {
                // the IntentionalDisconnectException is used to indicate that the disconnect was intentional, which
                // prevents the watchdog from trying to reconnect
                Client.Disconnect(message, new IntentionalDisconnectException(message));
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
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(ServerState), 200)]
        [ProducesResponseType(403)]
        public IActionResult Get()
        {
            return Ok(StateSnapshot.Value.Server);
        }
    }
}
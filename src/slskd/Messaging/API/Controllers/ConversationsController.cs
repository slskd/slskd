// <copyright file="ConversationsController.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Conversations.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ConversationsController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ConversationsController"/> class.
        /// </summary>
        /// <param name="applicationStateMonotor"></param>
        /// <param name="messagingService"></param>
        /// <param name="optionsSnapshot"></param>
        public ConversationsController(
            IStateMonitor<State> applicationStateMonotor,
            IMessagingService messagingService,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            ApplicationStateMonitor = applicationStateMonotor;
            Messages = messagingService;
            OptionsSnapshot = optionsSnapshot;
        }

        private IStateMonitor<State> ApplicationStateMonitor { get; }
        private IMessagingService Messages { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

        /// <summary>
        ///     Acknowledges the given message id for the given username.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">
        ///     A conversation with the specified username, or a message matching the specified id could not be found.
        /// </response>
        [HttpPut("{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Acknowledge([FromRoute]string username, [FromRoute]int id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var message = Messages.Conversations.FindMessageAsync(username, id);

            if (message == default)
            {
                return NotFound();
            }

            await Messages.Conversations.AcknowledgeMessageAsync(username, id);

            return Ok();
        }

        /// <summary>
        ///     Acknowledges all messages from the given username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A conversation with the specified username could not be found.</response>
        [HttpPut("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AcknowledgeAll([FromRoute]string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversation = Messages.Conversations.FindAsync(username);

            if (conversation == default)
            {
                return NotFound();
            }

            await Messages.Conversations.AcknowledgeAsync(username);

            return Ok();
        }

        /// <summary>
        ///     Closes the conversation associated with the given username.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The request completed successfully.</response>
        /// <response code="404">A conversation with the specified username could not be found.</response>
        [HttpDelete("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(404)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Close([FromRoute]string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversation = Messages.Conversations.FindAsync(username, includeInactive: false);

            if (conversation == default)
            {
                return NotFound();
            }

            await Messages.Conversations.RemoveAsync(username);

            return StatusCode(204);
        }

        /// <summary>
        ///     Gets all active conversations.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(List<Conversation>), 200)]
        public async Task<IActionResult> GetAll([FromQuery]bool unAckedOnly = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversations = await Messages.Conversations.ListAsync(c => c.IsActive && (!unAckedOnly || c.HasUnAcknowledgedMessages));

            return Ok(conversations);
        }

        /// <summary>
        ///     Gets the conversation associated with the specified username.
        /// </summary>
        /// <param name="username">The username associated with the desired conversation.</param>
        /// <param name="includeMessages"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A matching search was not found.</response>
        [HttpGet("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Conversation), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetByUsername([FromRoute]string username, [FromQuery]bool includeMessages = true)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversation = await Messages.Conversations.FindAsync(username, includeMessages);

            if (conversation == default)
            {
                return NotFound();
            }

            return Ok(conversation);
        }

        [HttpGet("{username}/messages")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(List<PrivateMessage>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMessagesByUsername([FromRoute]string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var messages = await Messages.Conversations.ListMessagesAsync(m => m.Username == username);

            if (!messages.Any())
            {
                return NotFound();
            }

            return Ok(messages);
        }

        /// <summary>
        ///     Sends a private message to the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <response code="201">The request completed successfully.</response>
        /// <response code="400">The specified message is null or empty.</response>
        [HttpPost("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Send([FromRoute]string username, [FromBody]string message)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(message))
            {
                return BadRequest();
            }

            await Messages.Conversations.SendMessageAsync(username, message);

            return StatusCode(201);
        }
    }
}
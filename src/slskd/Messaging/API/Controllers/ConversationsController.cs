// <copyright file="ConversationsController.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Messaging.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Users;

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
        /// <param name="applicationStateMonitor"></param>
        /// <param name="messagingService"></param>
        /// <param name="userService"></param>
        /// <param name="optionsSnapshot"></param>
        public ConversationsController(
            IStateMonitor<State> applicationStateMonitor,
            IMessagingService messagingService,
            IUserService userService,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            ApplicationStateMonitor = applicationStateMonitor;
            Messages = messagingService;
            Users = userService;
            OptionsSnapshot = optionsSnapshot;
        }

        private IStateMonitor<State> ApplicationStateMonitor { get; }
        private IMessagingService Messages { get; }
        private IUserService Users { get; }
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
        public async Task<IActionResult> Acknowledge([FromRoute, UrlEncoded] string username, [FromRoute] int id)
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
        public async Task<IActionResult> AcknowledgeAll([FromRoute, UrlEncoded] string username)
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
        public async Task<IActionResult> Close([FromRoute, UrlEncoded] string username)
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
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] bool unAcknowledgedOnly = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversations = await Messages.Conversations.ListAsync(c => includeInactive || c.IsActive);

            if (unAcknowledgedOnly)
            {
                conversations = conversations.Where(c => c.HasUnAcknowledgedMessages);
            }

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
        public async Task<IActionResult> GetByUsername([FromRoute, UrlEncoded] string username, [FromQuery] bool includeMessages = true)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversation = await Messages.Conversations.FindAsync(username, includeMessages: includeMessages);

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
        public async Task<IActionResult> GetMessagesByUsername([FromRoute, UrlEncoded] string username, [FromQuery] bool unAcknowledgedOnly = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var conversation = await Messages.Conversations.FindAsync(username, includeMessages: true);

            if (conversation == default)
            {
                return NotFound();
            }

            var messages = conversation.Messages;

            if (unAcknowledgedOnly)
            {
                messages = messages.Where(m => !m.IsAcknowledged);
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
        public async Task<IActionResult> Send([FromRoute, UrlEncoded] string username, [FromBody] string message)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (Users.IsBlacklisted(username))
            {
                return StatusCode(200);
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
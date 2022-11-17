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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Relay;
    using Soulseek;

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
        /// <param name="soulseekClient"></param>
        /// <param name="applicationStateMonotor"></param>
        /// <param name="tracker"></param>
        public ConversationsController(
            ISoulseekClient soulseekClient,
            IStateMonitor<State> applicationStateMonotor,
            IConversationTracker tracker,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            Client = soulseekClient;
            ApplicationStateMonitor = applicationStateMonotor;
            Tracker = tracker;
            OptionsSnapshot = optionsSnapshot;
        }

        private ISoulseekClient Client { get; }
        private IStateMonitor<State> ApplicationStateMonitor { get; }
        private IConversationTracker Tracker { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private bool IsAgent => OptionsSnapshot.Value.Relay.Mode.ToEnum<OperationMode>() == OperationMode.Agent;

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
            if (IsAgent)
            {
                return Forbid();
            }

            Tracker.Conversations.TryGetValue(username, out var conversation);

            if (conversation == default || !conversation.Any(p => p.Id == id))
            {
                return NotFound();
            }

            await Client.AcknowledgePrivateMessageAsync(id);
            return StatusCode(200);
        }

        /// <summary>
        ///     Acknowledges all messages for the given username.
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
            if (IsAgent)
            {
                return Forbid();
            }

            Tracker.Conversations.TryGetValue(username, out var conversation);

            if (conversation == default)
            {
                return NotFound();
            }

            var tasks = new List<Task>();

            foreach (var message in conversation.Where(p => !p.Acknowledged))
            {
                tasks.Add(Task.Run(async () =>
                {
                    await Client.AcknowledgePrivateMessageAsync(message.Id);
                    message.Acknowledged = true;
                }));
            }

            await Task.WhenAll(tasks);
            return StatusCode(200);
        }

        /// <summary>
        ///     Deletes the conversation associated with the given username.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The request completed successfully.</response>
        /// <response code="404">A conversation with the specified username could not be found.</response>
        [HttpDelete("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(404)]
        [ProducesResponseType(204)]
        public IActionResult Delete([FromRoute]string username)
        {
            if (IsAgent)
            {
                return Forbid();
            }

            var deleted = Tracker.Conversations.TryRemove(username, out _);

            if (deleted)
            {
                return StatusCode(204);
            }

            return StatusCode(404);
        }

        /// <summary>
        ///     Gets all tracked conversations.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, List<PrivateMessageResponse>>), 200)]
        public IActionResult GetAll()
        {
            if (IsAgent)
            {
                return Forbid();
            }

            var response = Tracker.Conversations.ToDictionary(
                entry => entry.Key,
                entry => entry.Value
                    .Select(pm => PrivateMessageResponse.FromPrivateMessage(pm, self: pm.Username == ApplicationStateMonitor.CurrentValue.User.Username))
                    .OrderBy(m => m.Timestamp));

            return Ok(response);
        }

        /// <summary>
        ///     Gets the conversation associated with the specified username.
        /// </summary>
        /// <param name="username">The username associated with the desired conversation.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A matching search was not found.</response>
        [HttpGet("{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(List<PrivateMessageResponse>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetByUsername([FromRoute]string username)
        {
            if (IsAgent)
            {
                return Forbid();
            }

            if (Tracker.TryGet(username, out var conversation))
            {
                var response = conversation
                    .Select(pm => PrivateMessageResponse.FromPrivateMessage(pm, self: pm.Username == ApplicationStateMonitor.CurrentValue.User.Username))
                    .OrderBy(m => m.Timestamp);

                return Ok(response);
            }

            return NotFound();
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
            if (IsAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(message))
            {
                return BadRequest();
            }

            await Client.SendPrivateMessageAsync(username, message);

            // append the outgoing message to the tracker
            Tracker.AddOrUpdate(username, new PrivateMessage()
            {
                Username = ApplicationStateMonitor.CurrentValue.User.Username,
                Timestamp = DateTime.UtcNow,
                Message = message,
                Acknowledged = true,
            });

            return StatusCode(201);
        }
    }
}
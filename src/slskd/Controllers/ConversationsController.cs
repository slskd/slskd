namespace slskd.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;
    using slskd.DTO;
    using slskd.Entities;
    using slskd.Trackers;

    /// <summary>
    ///     Conversations
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
        /// <param name="client"></param>
        /// <param name="tracker"></param>
        public ConversationsController(ISoulseekClient client, IConversationTracker tracker)
        {
            Client = client;
            Tracker = tracker;
        }

        private ISoulseekClient Client { get; }
        private IConversationTracker Tracker { get; }

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
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Acknowledge([FromRoute]string username, [FromRoute]int id)
        {
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
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AcknowledgeAll([FromRoute]string username)
        {
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
        [Authorize]
        [ProducesResponseType(404)]
        [ProducesResponseType(204)]
        public IActionResult Delete([FromRoute]string username)
        {
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
        [Authorize]
        [ProducesResponseType(typeof(Dictionary<string, List<PrivateMessageResponse>>), 200)]
        public IActionResult GetAll()
        {
            var response = Tracker.Conversations.ToDictionary(
                entry => entry.Key, 
                entry => entry.Value
                    .Select(pm => PrivateMessageResponse.FromPrivateMessage(pm, self: pm.Username == Client.Username))
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
        [Authorize]
        [ProducesResponseType(typeof(List<PrivateMessageResponse>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetByUsername([FromRoute]string username)
        {            
            if (Tracker.TryGet(username, out var conversation))
            {
                var response = conversation
                    .Select(pm => PrivateMessageResponse.FromPrivateMessage(pm, self: pm.Username == Client.Username))
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
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Send([FromRoute]string username, [FromBody]string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return BadRequest();
            }

            await Client.SendPrivateMessageAsync(username, message);

            Tracker.AddOrUpdate(username, new PrivateMessage()
            {
                Username = Client.Username,
                Timestamp = DateTime.UtcNow,
                Message = message,
                Acknowledged = true
            });

            return StatusCode(201);
        }
    }
}
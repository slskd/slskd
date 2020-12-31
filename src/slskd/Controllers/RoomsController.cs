namespace slskd.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using slskd.DTO;
    using slskd.Entities;
    using slskd.Trackers;

    /// <summary>
    ///     Rooms.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class RoomsController : ControllerBase
    {
        public RoomsController(ISoulseekClient client, IRoomTracker tracker)
        {
            Client = client;
            Tracker = tracker;
        }

        private ISoulseekClient Client { get; }
        private IRoomTracker Tracker { get; }

        /// <summary>
        ///     Gets all rooms.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("joined")]
        [Authorize]
        [ProducesResponseType(typeof(Dictionary<string, Dictionary<string, Room>>), 200)]
        public IActionResult GetAll()
        {
            return Ok(Tracker.Rooms.Keys);
        }

        /// <summary>
        ///     Gets the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpGet("joined/{roomName}")]
        [Authorize]
        [ProducesResponseType(typeof(Room), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetByRoomName([FromRoute]string roomName)
        {
            if (Tracker.TryGet(roomName, out var room))
            {
                return Ok(MapRoomToRoomResponse(room));
            }

            return NotFound();
        }

        /// <summary>
        ///     Sends a message to the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <response code="201">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpPost("joined/{roomName}/messages")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SendMessage([FromRoute]string roomName, [FromBody]string message)
        {
            if (Tracker.TryGet(roomName, out var _))
            {
                await Client.SendRoomMessageAsync(roomName, message);
                return StatusCode(StatusCodes.Status201Created);
            }

            return NotFound();
        }

        /// <summary>
        ///     Sets a ticker for the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <response code="201">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpPost("joined/{roomName}/ticker")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SetTicker([FromRoute] string roomName, [FromBody] string message)
        {
            if (Tracker.TryGet(roomName, out var _))
            {
                await Client.SetRoomTickerAsync(roomName, message);
                return StatusCode(StatusCodes.Status201Created);
            }

            return NotFound();
        }

        /// <summary>
        ///     Adds a member to a private room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="201">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpPost("joined/{roomName}/members")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AddRoomMember([FromRoute]string roomName, [FromBody]string username)
        {
            if (Tracker.TryGet(roomName, out  var _))
            {
                await Client.AddPrivateRoomMemberAsync(roomName, username);
                return StatusCode(StatusCodes.Status201Created);
            }

            return NotFound();
        }

        /// <summary>
        ///     Gets the current list of users for the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpGet("joined/{roomName}/users")]
        [Authorize]
        [ProducesResponseType(typeof(IList<UserData>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetUsersByRoomName([FromRoute]string roomName)
        {
            if (Tracker.TryGet(roomName, out var room))
            {
                var response = room.Users
                    .Select(user => UserDataResponse.FromUserData(user, self: user.Username == Client.Username));

                return Ok(response);
            }

            return NotFound();
        }

        /// <summary>
        ///     Gets the current list of messages for the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified roomName could not be found.</response>
        [HttpGet("joined/{roomName}/messages")]
        [Authorize]
        [ProducesResponseType(typeof(IList<RoomMessage>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetMessagesByRoomName([FromRoute]string roomName)
        {
            if (Tracker.TryGet(roomName, out var room))
            {
                var response = room.Messages
                    .Select(message => RoomMessageResponse.FromRoomMessage(message, self: message.Username == Client.Username));

                return Ok(response);
            }

            return NotFound();
        }

        /// <summary>
        ///     Gets a list of rooms from the server.
        /// </summary>
        /// <returns></returns>
        [HttpGet("available")]
        [Authorize]
        [ProducesResponseType(typeof(List<RoomInfo>), 200)]
        public async Task<IActionResult> GetRooms()
        {
            var list = await Client.GetRoomListAsync();

            var response = new List<RoomInfoResponse>();

            response.AddRange(list.Public.Select(r => RoomInfoResponse.FromRoomInfo(r)));
            response.AddRange(list.Private.Select(r => RoomInfoResponse.FromRoomInfo(r, isPrivate: true)));
            response.AddRange(list.Owned.Select(r => RoomInfoResponse.FromRoomInfo(r, isPrivate: true, isOwned: true)));

            foreach (var room in response)
            {
                room.IsModerated = list.ModeratedRoomNames.Contains(room.Name);
            }

            return Ok(response);
        }

        /// <summary>
        ///     Joins a room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        /// <response code="201">The request completed successfully.</response>
        /// <response code="304">The room has already been joined.</response>
        [HttpPost("joined/{roomName}")]
        [Authorize]
        [ProducesResponseType(typeof(Room), 201)]
        [ProducesResponseType(304)]
        public async Task<IActionResult> JoinRoom([FromRoute]string roomName)
        {
            if (Tracker.Rooms.ContainsKey(roomName))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            try
            {
                var roomData = await Client.JoinRoomAsync(roomName);
                var room = Room.FromRoomData(roomData);
                Tracker.TryAdd(roomName, room);

                return StatusCode(StatusCodes.Status201Created, MapRoomToRoomResponse(room));
            }
            catch (Exception ex)
            {
                if (ex.InnerException is RoomJoinForbiddenException)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, $"The server rejected your request to join {roomName}");
                }

                throw;
            }
        }

        /// <summary>
        ///     Leaves a room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        /// <response code="204">The request completed successfully.</response>
        /// <response code="404">The room has not been joined.</response>
        [HttpDelete("joined/{roomName}")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> LeaveRoom([FromRoute]string roomName)
        {
            if (!Tracker.Rooms.ContainsKey(roomName))
            {
                return StatusCode(StatusCodes.Status404NotFound);
            }

            await Client.LeaveRoomAsync(roomName);
            Tracker.TryRemove(roomName);

            return StatusCode(StatusCodes.Status204NoContent);
        }

        private RoomResponse MapRoomToRoomResponse(Room room)
        {
            bool IsSelf(string username)
            {
                return username == Client.Username;
            }

            var response = RoomResponse.FromRoom(room);
            response.Users = room.Users
                .Select(user => UserDataResponse.FromUserData(user, self: IsSelf(user.Username)));
            response.Messages = room.Messages
                .Select(message => RoomMessageResponse.FromRoomMessage(message, self: IsSelf(message.Username)));

            return response;
        }
    }
}
namespace slskd.API.DTO
{
    using System;
    using slskd.Entities;

    public class RoomMessageResponse
    {
        /// <summary>
        ///     The timestamp of the message.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     The username of the user who sent the message.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     The message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        ///     The room to which the message pertains.
        /// </summary>
        public string RoomName { get; set; }

        /// <summary>
        ///     A value indicating whether this user data belongs to the currently logged in user.
        /// </summary>
        public bool? Self { get; set; }

        public static RoomMessageResponse FromRoomMessage(RoomMessage roomMessage, bool self = false)
        {
            return new RoomMessageResponse()
            {
                Timestamp = roomMessage.Timestamp,
                Username = roomMessage.Username,
                Message = roomMessage.Message,
                RoomName = roomMessage.RoomName,
                Self = self ? self : (bool?)null,
            };
        }
    }
}

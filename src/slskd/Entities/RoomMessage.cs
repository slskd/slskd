namespace slskd.Entities
{
    using Soulseek;
    using System;

    /// <summary>
    ///     A message sent to a room.
    /// </summary>
    public class RoomMessage
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

        public static RoomMessage FromEventArgs(RoomMessageReceivedEventArgs eventArgs, DateTime? timestamp = null)
        {
            return new RoomMessage()
            {
                Timestamp = timestamp ?? DateTime.UtcNow,
                Username = eventArgs.Username,
                Message = eventArgs.Message,
                RoomName = eventArgs.RoomName,
            };
        }
    }
}

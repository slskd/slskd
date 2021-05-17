namespace slskd.API.DTO
{
    using System;
    using slskd.Entities;

    public class PrivateMessageResponse
    {
        /// <summary>
        ///     A value indicating whether the message has been acknowledged.
        /// </summary>
        public bool Acknowledged { get; set; } = false;

        /// <summary>
        ///     The unique message id, used to acknowledge receipt.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        ///     The message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        ///     A value indicating whether the message was replayed.
        /// </summary>
        public bool Replayed { get; set; }

        /// <summary>
        ///     The UTC timestamp of the message.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     The username of the user who sent the message.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     A value indicating whether this message was sent by the currently logged in user.
        /// </summary>
        public bool? Self { get; set; }

        public static PrivateMessageResponse FromPrivateMessage(PrivateMessage privateMessage, bool self = false)
        {
            return new PrivateMessageResponse()
            {
                Id = privateMessage.Id,
                Timestamp = privateMessage.Timestamp,
                Username = privateMessage.Username,
                Message = privateMessage.Message,
                Acknowledged = privateMessage.Acknowledged,
                Replayed = privateMessage.Replayed,
                Self = self ? self: (bool?)null,
            };
        }
    }
}

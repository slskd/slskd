namespace slskd.Entities
{
    using Soulseek;
    using System;

    /// <summary>
    ///     A private message.
    /// </summary>
    public class PrivateMessage
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

        public static PrivateMessage FromEventArgs(PrivateMessageReceivedEventArgs eventArgs)
        {
            return new PrivateMessage()
            {
                Id = eventArgs.Id,
                Timestamp = eventArgs.Timestamp,
                Username = eventArgs.Username,
                Message = eventArgs.Message,
                Acknowledged = false,
                Replayed = eventArgs.Replayed
            };
        }
    }
}
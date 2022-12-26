// <copyright file="RoomMessage.cs" company="slskd Team">
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

namespace slskd.Messaging
{
    using System;
    using Soulseek;

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

        /// <summary>
        ///     Gets or sets the message direction.
        /// </summary>
        public MessageDirection Direction { get; set; }

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

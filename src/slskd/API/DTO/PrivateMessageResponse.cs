// <copyright file="PrivateMessageResponse.cs" company="slskd Team">
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

namespace slskd.API.DTO
{
    using slskd.Messaging;
    using System;

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
                Self = self ? self : (bool?)null,
            };
        }
    }
}

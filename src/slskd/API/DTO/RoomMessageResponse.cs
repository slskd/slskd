// <copyright file="RoomMessageResponse.cs" company="slskd Team">
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

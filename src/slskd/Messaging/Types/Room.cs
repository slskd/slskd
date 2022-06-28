// <copyright file="Room.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.Linq;
    using Soulseek;

    public class Room
    {
        /// <summary>
        ///     The room name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     A value indicating whether the room is private.
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        ///     The number of operators in the room, if private.
        /// </summary>
        public int? OperatorCount { get; set; }

        /// <summary>
        ///     The operators in the room, if private.
        /// </summary>
        public IList<string> Operators { get; set; }

        /// <summary>
        ///     The owner of the room, if private.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        ///     The list of users in the room.
        /// </summary>
        public IList<UserData> Users { get; set; } = new List<UserData>();

        /// <summary>
        ///     The list of messages.
        /// </summary>
        public IList<RoomMessage> Messages { get; set; } = new List<RoomMessage>();

        public static Room FromRoomData(RoomData roomData)
        {
            return new Room()
            {
                Name = roomData.Name,
                IsPrivate = roomData.IsPrivate,
                OperatorCount = roomData.OperatorCount,
                Operators = roomData.Operators?.ToList(),
                Owner = roomData.Owner,
                Users = roomData.Users?.ToList(),
                Messages = new List<RoomMessage>(),
            };
        }
    }
}

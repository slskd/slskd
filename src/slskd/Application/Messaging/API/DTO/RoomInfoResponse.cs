// <copyright file="RoomInfoResponse.cs" company="slskd Team">
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

namespace slskd.Messaging.API
{
    using Soulseek;

    public class RoomInfoResponse
    {
        /// <summary>
        ///     Gets the room name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     Gets the number of users in the room.
        /// </summary>
        public int UserCount { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is private.
        /// </summary>
        public bool IsPrivate { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is owned by the currently logged in user.
        /// </summary>
        public bool IsOwned { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is moderated by the currently logged in user.
        /// </summary>
        public bool IsModerated { get; set; }

        public static RoomInfoResponse FromRoomInfo(RoomInfo info, bool isPrivate = false, bool isOwned = false)
        {
            return new RoomInfoResponse()
            {
                Name = info.Name,
                UserCount = info.UserCount,
                IsPrivate = isPrivate,
                IsOwned = isOwned,
            };
        }
    }
}

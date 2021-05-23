// <copyright file="IRoomTracker.cs" company="slskd Team">
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
    using System.Collections.Concurrent;
    using Soulseek;

    /// <summary>
    ///     Tracks rooms.
    /// </summary>
    public interface IRoomTracker
    {
        /// <summary>
        ///     Gets tracked rooms.
        /// </summary>
        ConcurrentDictionary<string, Room> Rooms { get; }

        /// <summary>
        ///     Adds a room and appends the specified <paramref name="message"/>, or just appends the message if the room exists.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="message"></param>
        void AddOrUpdateMessage(string roomName, RoomMessage message);

        /// <summary>
        ///     Adds the specified room to the tracker.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="room"></param>
        void TryAdd(string roomName, Room room);

        /// <summary>
        ///     Adds the specified <paramref name="userData"/> to the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="userData"></param>
        void TryAddUser(string roomName, UserData userData);

        /// <summary>
        ///     Returns the list of messages for the specified <paramref name="roomName"/>, if it is tracked.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="room"></param>
        /// <returns></returns>
        bool TryGet(string roomName, out Room room);

        /// <summary>
        ///     Removes a tracked room.
        /// </summary>
        /// <param name="roomName"></param>
        void TryRemove(string roomName);

        /// <summary>
        ///     Removes the specified <paramref name="username"/> from the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="username"></param>
        void TryRemoveUser(string roomName, string username);
    }
}
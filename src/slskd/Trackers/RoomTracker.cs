// <copyright file="RoomTracker.cs" company="slskd Team">
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

namespace slskd.Trackers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using slskd.Entities;
    using Soulseek;

    /// <summary>
    ///     Tracks rooms.
    /// </summary>
    public class RoomTracker : IRoomTracker
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RoomTracker"/> class.
        /// </summary>
        /// <param name="messageLimit"></param>
        public RoomTracker(int messageLimit = 25)
        {
            MessageLimit = messageLimit;
        }

        /// <summary>
        ///     Tracked rooms.
        /// </summary>
        public ConcurrentDictionary<string, Room> Rooms { get; } = new ConcurrentDictionary<string, Room>();

        private int MessageLimit { get; }

        /// <summary>
        ///     Adds a room and appends the specified <paramref name="message"/>, or just appends the message if the room exists.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="message"></param>
        public void AddOrUpdateMessage(string roomName, RoomMessage message)
        {
            Rooms.AddOrUpdate(roomName, new Room() { Messages = new List<RoomMessage>() { message } }, (_, room) =>
            {
                if (room.Messages.Count >= MessageLimit)
                {
                    room.Messages = room.Messages.TakeLast(MessageLimit - 1).ToList();
                }

                room.Messages.Add(message);
                return room;
            });
        }

        /// <summary>
        ///     Adds the specified room to the tracker
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="room"></param>
        public void TryAdd(string roomName, Room room) => Rooms.TryAdd(roomName, room);

        /// <summary>
        ///     Adds the specified <paramref name="userData"/> to the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="userData"></param>
        public void TryAddUser(string roomName, UserData userData)
        {
            if (Rooms.TryGetValue(roomName, out var room))
            {
                room.Users.Add(userData);
            }
        }

        /// <summary>
        ///     Removes a tracked room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="room"></param>
        public bool TryGet(string roomName, out Room room) => Rooms.TryGetValue(roomName, out room);

        /// <summary>
        ///     Returns the list of messages for the specified <paramref name="roomName"/>, if it is tracked.
        /// </summary>
        /// <param name="roomName"></param>
        public void TryRemove(string roomName) => Rooms.TryRemove(roomName, out _);

        /// <summary>
        ///     Removes the specified <paramref name="username"/> from the specified room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="username"></param>
        public void TryRemoveUser(string roomName, string username)
        {
            if (Rooms.TryGetValue(roomName, out var room))
            {
                room.Users = room.Users.Where(u => u.Username != username).ToList();
            }
        }
    }
}
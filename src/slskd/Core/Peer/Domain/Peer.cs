// <copyright file="Peer.cs" company="slskd Team">
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

namespace slskd.Peer
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Net;
    using UserPresence = Soulseek.UserPresence;

    public class Peer
    {
        [Key]
        public string Username { get; init; }
        public string Description { get; init; }
        public bool HasFreeUploadSlot { get; init; }
        public bool HasPicture { get; init; }
        public byte[] Picture { get; init; }
        public int QueueLength { get; init; }
        public int UploadSlots { get; init; }
        public bool IsPrivileged { get; init; }
        public UserPresence Presence { get; init; }
        public IPAddress IPAddress { get; init; }
        public int Port { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}

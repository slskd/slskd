// <copyright file="Info.cs" company="slskd Team">
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
    using Soulseek;

    public class Info
    {
        [Key]
        public string Username { get; init; }
        public string Description { get; init; }
        public bool HasFreeUploadSlot { get; init; }
        public bool HasPicture { get; init; }
        public byte[] Picture { get; init; }
        public int QueueLength { get; init; }
        public int UploadSlots { get; init; }
        public DateTime UpdatedAt { get; init; }

        public static Info FromSoulseekUserInfo(string username, UserInfo info)
        {
            return new Info()
            {
                Username = username,
                Description = info.Description,
                HasFreeUploadSlot = info.HasFreeUploadSlot,
                HasPicture = info.HasPicture,
                Picture = info.Picture,
                QueueLength = info.QueueLength,
                UploadSlots = info.UploadSlots,
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }
}

// <copyright file="Extensions.cs" company="slskd Team">
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

namespace slskd.Users
{
    using Soulseek;

    /// <summary>
    ///     Users extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        ///     Converts <see cref="Soulseek.UserStatistics"/> to <see cref="Statistics"/>.
        /// </summary>
        /// <param name="u">The UserStatistics instance to convert.</param>
        /// <returns>The converted instance.</returns>
        public static Statistics ToStatistics(this UserStatistics u) => new()
        {
            AverageSpeed = u.AverageSpeed,
            DirectoryCount = u.DirectoryCount,
            FileCount = u.FileCount,
            UploadCount = u.UploadCount,
        };

        /// <summary>
        ///     Converts <see cref="Soulseek.UserStatus"/> to <see cref="Status"/>.
        /// </summary>
        /// <param name="s">The UserStatus instance to convert.</param>
        /// <returns>The converted instance.</returns>
        public static Status ToStatus(this UserStatus s) => new()
        {
            IsPrivileged = s.IsPrivileged,
            Presence = s.Presence,
        };

        /// <summary>
        ///     Converts <see cref="Soulseek.UserInfo"/> to <see cref="Info"/>.
        /// </summary>
        /// <param name="i">The UserInfo instance to convert.</param>
        /// <returns>The converted instance.</returns>
        public static Info ToInfo(this UserInfo i) => new()
        {
            Description = i.Description,
            HasFreeUploadSlot = i.HasFreeUploadSlot,
            HasPicture = i.HasPicture,
            Picture = i.Picture,
            QueueLength = i.QueueLength,
            UploadSlots = i.UploadSlots,
        };
    }
}

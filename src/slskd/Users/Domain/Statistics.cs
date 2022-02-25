// <copyright file="Statistics.cs" company="slskd Team">
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
    public record Statistics
    {
        /// <summary>
        ///     Gets the average upload speed of the user.
        /// </summary>
        public int AverageSpeed { get; init; }

        /// <summary>
        ///     Gets the number of directories shared by the user.
        /// </summary>
        public int DirectoryCount { get; init; }

        /// <summary>
        ///     Gets the number of files shared by the user.
        /// </summary>
        public int FileCount { get; init; }

        /// <summary>
        ///     Gets the number of uploads tracked by the server for this user.
        /// </summary>
        public long UploadCount { get; init; }
    }
}

// <copyright file="StorageMode.cs" company="slskd Team">
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

namespace slskd.Shares
{
    /// <summary>
    ///     Storage mode.
    /// </summary>
    public enum StorageMode
    {
        /// <summary>
        ///     Stored in memory.
        /// </summary>
        Memory = 0,

        /// <summary>
        ///     Stored on disk.
        /// </summary>
        Disk = 1,
    }
}

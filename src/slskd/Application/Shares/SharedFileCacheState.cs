// <copyright file="SharedFileCacheState.cs" company="slskd Team">
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
    using System;

    /// <summary>
    ///     Share cache state.
    /// </summary>
    public record SharedFileCacheState()
    {
        /// <summary>
        ///     Gets a value indicating whether the cache is ready to be used.
        /// </summary>
        public bool Ready { get; init; } = true;

        /// <summary>
        ///     Gets the current fill progress.
        /// </summary>
        public double FillProgress { get; init; } = 1;

        /// <summary>
        ///     Gets the number of cached directories.
        /// </summary>
        public int Directories { get; init; }

        /// <summary>
        ///     Gets the number of cached files.
        /// </summary>
        public int Files { get; init; }

        /// <summary>
        ///     Gets the UTC timestamp of the last fill.
        /// </summary>
        public DateTime? LastFilled { get; init; }
    }
}

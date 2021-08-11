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

namespace slskd
{
    /// <summary>
    ///     Share cache state.
    /// </summary>
    public record SharedFileCacheState
    {
        /// <summary>
        ///     Gets a value indicating whether the cache is being filled.
        /// </summary>
        public bool Filling { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether the cache is faulted.
        /// </summary>
        public bool Faulted { get; init; } = false;

        /// <summary>
        ///     Gets the current fill progress.
        /// </summary>
        public double FillProgress { get; init; }

        /// <summary>
        ///     Gets the number of cached directories.
        /// </summary>
        public int Directories { get; init; }

        /// <summary>
        ///     Gets the number of cached files.
        /// </summary>
        public int Files { get; init; }

        /// <summary>
        ///     Gets the number of directories excluded by filters.
        /// </summary>
        public int ExcludedDirectories { get; init; }
    }
}
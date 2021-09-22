// <copyright file="IBrowseTracker.cs" company="slskd Team">
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
    using System.Collections.Concurrent;
    using Soulseek;

    /// <summary>
    ///     Tracks browse operations.
    /// </summary>
    public interface IBrowseTracker
    {
        /// <summary>
        ///     Tracked browse operations.
        /// </summary>
        ConcurrentDictionary<string, BrowseProgressUpdatedEventArgs> Browses { get; }

        /// <summary>
        ///     Adds or updates a tracked browse operation.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        void AddOrUpdate(string username, BrowseProgressUpdatedEventArgs progress);

        /// <summary>
        ///     Gets the browse progress for the specified user.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        bool TryGet(string username, out BrowseProgressUpdatedEventArgs progress);

        /// <summary>
        ///     Removes a tracked browse operation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        void TryRemove(string username);
    }
}
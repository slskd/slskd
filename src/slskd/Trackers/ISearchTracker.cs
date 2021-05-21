// <copyright file="ISearchTracker.cs" company="slskd Team">
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
    using System;
    using System.Collections.Concurrent;
    using Soulseek;

    /// <summary>
    ///     Tracks active searches.
    /// </summary>
    public interface ISearchTracker
    {
        /// <summary>
        ///     Gets active searches.
        /// </summary>
        ConcurrentDictionary<Guid, Search> Searches { get; }

        /// <summary>
        ///     Adds or updates a tracked search.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="args"></param>
        void AddOrUpdate(Guid id, SearchEventArgs args);

        /// <summary>
        ///     Removes all tracked searches.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Removes a tracked search.
        /// </summary>
        /// <param name="id"></param>
        void TryRemove(Guid id);
    }
}
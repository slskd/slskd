// <copyright file="SearchTracker.cs" company="slskd Team">
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

namespace slskd.Search
{
    using System;
    using System.Collections.Concurrent;
    using Soulseek;

    /// <summary>
    ///     Tracks active searches.
    /// </summary>
    public class SearchTracker : ISearchTracker
    {
        /// <summary>
        ///     Gets active searches.
        /// </summary>
        public ConcurrentDictionary<Guid, Search> Searches { get; private set; } =
            new ConcurrentDictionary<Guid, Search>();

        /// <summary>
        ///     Adds or updates a tracked search.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="args"></param>
        public void AddOrUpdate(Guid id, SearchEventArgs args)
        {
            Searches.AddOrUpdate(id, args.Search, (token, search) => args.Search);
        }

        /// <summary>
        ///     Removes all tracked searches.
        /// </summary>
        public void Clear()
        {
            Searches.Clear();
        }

        /// <summary>
        ///     Removes a tracked search.
        /// </summary>
        /// <param name="id"></param>
        public void TryRemove(Guid id)
        {
            Searches.TryRemove(id, out _);
        }
    }
}
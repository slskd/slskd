// <copyright file="SearchResponseCache.cs" company="slskd Team">
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
    using Microsoft.Extensions.Caching.Memory;
    using Soulseek;

    /// <summary>
    ///     Caches undelivered search responses.
    /// </summary>
    public class SearchResponseCache : ISearchResponseCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchResponseCache"/> class.
        /// </summary>
        public SearchResponseCache()
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
        }

        private IMemoryCache Cache { get; }

        /// <summary>
        ///     Caches or updates a response.
        /// </summary>
        /// <param name="responseToken">The token for which the response is to be added or updated.</param>
        /// <param name="response">The response and context to cache.</param>
        public void AddOrUpdate(int responseToken, (string Username, int Token, string Query, Soulseek.SearchResponse SearchResponse) response)
        {
            Cache.Set(responseToken, response, TimeSpan.FromMinutes(3));
        }

        /// <summary>
        ///     Attempts to fetch a cached response and context for the specified <paramref name="responseToken"/>.
        /// </summary>
        /// <param name="responseToken">The token for the cached response.</param>
        /// <param name="response">The cached response and context, if present.</param>
        /// <returns>A value indicating whether a response for the specified responseToken is cached.</returns>
        public bool TryGet(int responseToken, out (string Username, int Token, string Query, Soulseek.SearchResponse SearchResponse) response)
        {
            return Cache.TryGetValue(responseToken, out response);
        }

        /// <summary>
        ///     Attempts to remove a cached Soulseek.SearchResponse and context for the specified <paramref name="responseToken"/>.
        /// </summary>
        /// <param name="responseToken">The token for the cached response.</param>
        /// <param name="response">The cached response and context, if present.</param>
        /// <returns>A value indicating whether a response for the specified responseToken was removed.</returns>
        public bool TryRemove(int responseToken, out (string Username, int Token, string Query, Soulseek.SearchResponse SearchResponse) response)
        {
            if (Cache.TryGetValue(responseToken, out response))
            {
                Cache.Remove(responseToken);
                return true;
            }

            return false;
        }
    }
}
// <copyright file="SearchResponseCache.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
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
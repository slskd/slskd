// <copyright file="UserEndPointCache.cs" company="JP Dillingham">
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

namespace slskd
{
    using System;
    using System.Net;
    using Microsoft.Extensions.Caching.Memory;
    using Soulseek;

    /// <summary>
    ///     Caches user EndPoints.
    /// </summary>
    public class UserEndPointCache : IUserEndPointCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserEndPointCache"/> class.
        /// </summary>
        public UserEndPointCache()
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
        }

        private IMemoryCache Cache { get; }

        /// <summary>
        ///     Caches or updates an entry.
        /// </summary>
        /// <param name="username">The username for which to cache the endpoint.</param>
        /// <param name="endPoint">The endpoint to cache.</param>
        public void AddOrUpdate(string username, IPEndPoint endPoint)
        {
            Cache.Set(username, endPoint, TimeSpan.FromSeconds(60));
        }

        /// <summary>
        ///     Gets the cached endpoint for the specified <paramref name="username"/>, if it exists.
        /// </summary>
        /// <param name="username">The username for which to retrieve the endpoint.</param>
        /// <param name="endPoint">The cached endpoint, if it exists.</param>
        /// <returns>A value indicating whether the endpoint was found in the cache.</returns>
        public bool TryGet(string username, out IPEndPoint endPoint)
        {
            return Cache.TryGetValue(username, out endPoint);
        }
    }
}
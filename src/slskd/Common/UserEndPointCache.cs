// <copyright file="UserEndPointCache.cs" company="slskd Team">
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
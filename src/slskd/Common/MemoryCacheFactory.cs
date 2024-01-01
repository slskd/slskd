// <copyright file="MemoryCacheFactory.cs" company="slskd Team">
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
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    ///     Factory for <see cref="IMemoryCache"/>.
    /// </summary>
    /// <remarks>
    ///     Avoids lifetime issues with the built in instance of <see cref="IMemoryCache"/>.
    /// </remarks>
    public class MemoryCacheFactory
    {
        /// <summary>
        ///     Creates a new instance of <see cref="IMemoryCache"/> with the specified <paramref name="options"/>.
        /// </summary>
        /// <param name="options">The optionally specified options for the new cache.</param>
        /// <returns>The created instance.</returns>
        public virtual IMemoryCache Create(MemoryCacheOptions options = null) => new MemoryCache(options ?? new MemoryCacheOptions());
    }
}

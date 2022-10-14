// <copyright file="IShareRepositoryFactory.cs" company="slskd Team">
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
    /// <summary>
    ///     Persistent storage of a shared file cache.
    /// </summary>
    public interface IShareRepositoryFactory
    {
        /// <summary>
        ///     Create a repository for the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the agent.</param>
        /// <returns>The created repository.</returns>
        IShareRepository CreateFromHost(string name);

        /// <summary>
        ///     Create a repository backup for the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the agent.</param>
        /// <returns>The created repository.</returns>
        IShareRepository CreateFromHostBackup(string name);

        /// <summary>
        ///     Create a repository for the specified <paramref name="filename"/>.
        /// </summary>
        /// <param name="filename">The fully qualified path of the filename.</param>
        /// <returns>The created repository.</returns>
        IShareRepository CreateFromFile(string filename);
    }
}

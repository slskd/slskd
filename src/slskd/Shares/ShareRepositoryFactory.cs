// <copyright file="ShareRepositoryFactory.cs" company="slskd Team">
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

using System.IO;

namespace slskd.Shares
{
    /// <summary>
    ///     Persistent storage of a shared file cache.
    /// </summary>
    public class SqliteShareRepositoryFactory : IShareRepositoryFactory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteShareRepositoryFactory"/> class.
        /// </summary>
        /// <param name="optionsAtStartup"></param>
        public SqliteShareRepositoryFactory(
            OptionsAtStartup optionsAtStartup)
        {
            StorageMode = optionsAtStartup.Shares.Cache.StorageMode.ToEnum<StorageMode>();
        }

        private StorageMode StorageMode { get; }

        /// <summary>
        ///     Create a repository for the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the agent.</param>
        /// <returns>The created repository.</returns>
        public IShareRepository CreateFromHost(string name)
        {
            if (StorageMode == StorageMode.Memory)
            {
                return new SqliteShareRepository($"Data Source=file:shares.{name}?mode=memory;Cache=shared");
            }

            var file = Path.Combine(Program.DataDirectory, $"shares.{name}.db");
            return new SqliteShareRepository($"Data Source={file};Cache=shared");
        }

        /// <summary>
        ///     Create a repository backup for the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the agent.</param>
        /// <returns>The created repository.</returns>
        public IShareRepository CreateFromHostBackup(string name)
        {
            var file = Path.Combine(Program.DataDirectory, $"shares.{name}.bak.db");
            return new SqliteShareRepository($"Data Source={file}");
        }

        /// <summary>
        ///     Create a repository for the specified <paramref name="filename"/>.
        /// </summary>
        /// <param name="filename">The fully qualified path of the filename.</param>
        /// <param name="pooling">A value indicating whether pooling should be enabled.</param>
        /// <returns>The created repository.</returns>
        public IShareRepository CreateFromFile(string filename, bool pooling = false)
            => new SqliteShareRepository($"Data Source={filename};Pooling={pooling}");
    }
}

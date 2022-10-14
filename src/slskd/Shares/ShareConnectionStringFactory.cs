// <copyright file="ShareConnectionStringFactory.cs" company="slskd Team">
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
    using System;

    /// <summary>
    ///     Creates connection strings for share cache databases.
    /// </summary>
    public class ShareConnectionStringFactory
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ShareConnectionStringFactory"/> class.
        /// </summary>
        /// <param name="createFromFile">The function used to create a connection string for a share cache database.</param>
        /// <param name="createFromMemory">The function used to create an in-memory connection string for a share cache database.</param>
        /// <param name="createBackupFromFile">The function used to crate a connection string for a share cache backup database.</param>
        public ShareConnectionStringFactory(
            Func<string, string> createFromFile,
            Func<string, string> createFromMemory,
            Func<string, string> createBackupFromFile)
        {
            CreateFromFile = createFromFile;
            CreateFromMemory = createFromMemory;
            CreateBackupFromFile = createBackupFromFile;
        }

        /// <summary>
        ///     Gets a function used to create a connection string for a share cache database.
        /// </summary>
        public Func<string, string> CreateFromFile { get; }

        /// <summary>
        ///     Gets a function used to create an in-memory connection string for a share cache database.
        /// </summary>
        public Func<string, string> CreateFromMemory { get; }

        /// <summary>
        ///     Gets a function used to create a connection string for a share cache backup database.
        /// </summary>
        public Func<string, string> CreateBackupFromFile { get; }
    }
}

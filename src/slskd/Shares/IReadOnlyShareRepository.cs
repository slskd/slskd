// <copyright file="IReadOnlyShareRepository.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using Soulseek;

    /// <summary>
    ///     Read-only persistent storage of shared files and metadata.
    /// </summary>
    public interface IReadOnlyShareRepository
    {
        /// <summary>
        ///     Gets the connection string for this repository.
        /// </summary>
        string ConnectionString { get; }

        /// <summary>
        ///     Counts the number of directories in the database.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for counting subdirectories.</param>
        /// <returns>The number of directories.</returns>
        int CountDirectories(string parentDirectory = null);

        /// <summary>
        ///     Counts the number of files in the database.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for counting files in a subdirectory.</param>
        /// <returns>The number of files.</returns>
        int CountFiles(string parentDirectory = null);

        /// <summary>
        ///     Finds the filename of the file matching the specified <paramref name="maskedFilename"/>.
        /// </summary>
        /// <param name="maskedFilename">The fully qualified remote path of the file.</param>
        /// <returns>The filename, if found.</returns>
        string FindFilename(string maskedFilename);

        /// <summary>
        ///     Lists all directories.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for listing subdirectories.</param>
        /// <returns>The list of directories.</returns>
        IEnumerable<string> ListDirectories(string parentDirectory = null);

        /// <summary>
        ///     Lists all files.
        /// </summary>
        /// <param name="parentDirectory">The optional parent directory.</param>
        /// <param name="includeFullPath">A value indicating whether the fully qualified path should be returned.</param>
        /// <returns>The list of files.</returns>
        IEnumerable<File> ListFiles(string parentDirectory = null, bool includeFullPath = false);

        /// <summary>
        ///     Searches the database for files matching the specified <paramref name="query"/>.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>The list of matching files.</returns>
        IEnumerable<File> Search(SearchQuery query);
    }
}

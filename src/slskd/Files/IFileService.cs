// <copyright file="IFileService.cs" company="slskd Team">
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

namespace slskd.Files
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    ///     Manages files on disk.
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        ///     Lists the contents in the specified <paramref name="parentDirectory"/>, optionally applying the
        ///     specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="parentDirectory">The directory from which to start the listing.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found contents.</returns>
        /// <exception cref="InvalidDirectoryException">
        ///     Thrown if the specified directory is not rooted in an allowed directory.
        /// </exception>
        /// <exception cref="NotFoundException">Thrown if the specified directory does not exist.</exception>
        Task<IEnumerable<FilesystemDirectory>> ListContentsAsync(string parentDirectory, EnumerationOptions enumerationOptions = null);
    }
}
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
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security;
    using System.Threading.Tasks;
    using OneOf;

    /// <summary>
    ///     Manages files on disk.
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        ///     Deletes the specified <paramref name="directories"/>.
        /// </summary>
        /// <remarks>
        ///     Returns a dictionary keyed on directory name and containing a result for each specified directory. Exceptions are
        ///     contained in the result, and are not thrown.
        /// </remarks>
        /// <param name="directories">The directories to delete.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the specified directories have a relative path.</exception>
        /// <exception cref="ArgumentException">
        ///     Thrown if any of the directories is an exact match for an application-controlled directory.
        /// </exception>
        /// <exception cref="NotFoundException">Thrown if a specified directory does not exist.</exception>
        /// <exception cref="SecurityException">Thrown if a specified directory is restricted.</exception>
        Task<Dictionary<string, OneOf<bool, Exception>>> DeleteDirectoriesAsync(params string[] directories);

        /// <summary>
        ///     Deletes the specified <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     Returns a dictionary keyed on directory name and containing a result for each specified directory. Exceptions are
        ///     contained in the result, and are not thrown.
        /// </remarks>
        /// <param name="files">The list of files to delete.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the specified files have a relative path.</exception>
        /// <exception cref="NotFoundException">Thrown if a specified file does not exist.</exception>
        /// <exception cref="SecurityException">Thrown if a specified file is restricted.</exception>
        Task<Dictionary<string, OneOf<bool, Exception>>> DeleteFilesAsync(params string[] files);

        /// <summary>
        ///     Lists the contents in the specified <paramref name="directory"/>, optionally applying the specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="directory">The directory from which to start the listing.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found contents.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified directory has a relative path.</exception>
        /// <exception cref="NotFoundException">Thrown if the specified directory does not exist.</exception>
        /// <exception cref="SecurityException">Thrown if the specified root directory is restricted.</exception>
        Task<FilesystemDirectory> ListContentsAsync(string directory, EnumerationOptions enumerationOptions = null);
    }
}
// <copyright file="FilesystemDirectory.cs" company="slskd Team">
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

    /// <summary>
    ///     A file directory on the host filesystem.
    /// </summary>
    public record FilesystemDirectory
    {
        /// <summary>
        ///     The name of the directory.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     The fully qualified name of the directory.
        /// </summary>
        public string FullName { get; init; }

        /// <summary>
        ///     The directories' attributes.
        /// </summary>
        public FileAttributes Attributes { get; init; }

        /// <summary>
        ///     The timestamp at which the directory was created.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        ///     The timestamp at which the directory was last modified.
        /// </summary>
        public DateTime ModifiedAt { get; init; }

        /// <summary>
        ///     The files within the directory.
        /// </summary>
        public IEnumerable<FilesystemFile> Files { get; init; }

        /// <summary>
        ///     The directories within the directory.
        /// </summary>
        public IEnumerable<FilesystemDirectory> Directories { get; init; }

        /// <summary>
        ///     Maps a <see cref="FilesystemDirectory"/> from the specified <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="i">The DirectoryInfo instance from which to map.</param>
        /// <returns>A new instance of FilesystemDirectory.</returns>
        public static FilesystemDirectory FromDirectoryInfo(DirectoryInfo i)
        {
            return new FilesystemDirectory
            {
                Name = i.Name,
                FullName = i.FullName,
                Attributes = i.Attributes,
                CreatedAt = i.CreationTimeUtc,
                ModifiedAt = i.LastWriteTimeUtc,
            };
        }
    }
}

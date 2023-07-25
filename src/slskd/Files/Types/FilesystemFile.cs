// <copyright file="FilesystemFile.cs" company="slskd Team">
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
    using System.IO;

    /// <summary>
    ///     A file on the host filesystem.
    /// </summary>
    public record FilesystemFile
    {
        /// <summary>
        ///     The name of the file.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     The fully qualified name of the file.
        /// </summary>
        public string FullName { get; init; }

        /// <summary>
        ///     The size of the file, in bytes.
        /// </summary>
        public long Length { get; init; }

        /// <summary>
        ///     The file's attributes.
        /// </summary>
        public FileAttributes Attributes { get; init; }

        /// <summary>
        ///     The timestamp at which the file was created.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        ///     The timestamp at which the file was last modified.
        /// </summary>
        public DateTime ModifiedAt { get; init; }

        /// <summary>
        ///     Maps a <see cref="FilesystemFile"/> from the specified <see cref="FileInfo"/>.
        /// </summary>
        /// <param name="i">The FileInfo instance from which to map.</param>
        /// <returns>A new instance of FilesystemFile.</returns>
        public static FilesystemFile FromFileInfo(FileInfo i)
        {
            return new FilesystemFile
            {
                Name = i.Name,
                FullName = i.FullName,
                Length = i.Length,
                Attributes = i.Attributes,
                CreatedAt = i.CreationTimeUtc,
                ModifiedAt = i.LastWriteTimeUtc,
            };
        }
    }
}

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

    public record FilesystemDirectory
    {
        public string Name { get; init; }
        public string FullName { get; init; }
        public FileAttributes Attributes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime ModifiedAt { get; init; }
        public IEnumerable<FilesystemFile> Files { get; init; }

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

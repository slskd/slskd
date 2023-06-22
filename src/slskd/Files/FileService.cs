// <copyright file="FileService.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Files
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    ///     Manages files on disk.
    /// </summary>
    public class FileService : IFileService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FileService"/> class.
        /// </summary>
        public FileService(
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
        }

        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

        /// <summary>
        ///     Lists the contents in the specified <paramref name="parentDirectory"/>, optionally applying the
        ///     specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="rootDirectory">The root directory.</param>
        /// <param name="parentDirectory">An optional subdirectory from which to start the listing.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found contents.</returns>
        /// <exception cref="NotFoundException">Thrown if the specified directory does not exist.</exception>
        /// <exception cref="ForbiddenException">Thrown if the specified directory is restricted.</exception>
        public async Task<IEnumerable<FilesystemDirectory>> ListContentsAsync(string rootDirectory, string parentDirectory = null, EnumerationOptions enumerationOptions = null)
        {
            var configuredDirectories = OptionsSnapshot.Value.Directories;
            var allowedDirectories = new[] { configuredDirectories.Incomplete, configuredDirectories.Downloads };

            if (!allowedDirectories.Any(d => rootDirectory == d))
            {
                throw new ForbiddenException($"For security reasons, only the configured Downloads and Incomplete directories can be listed");
            }

            var fullDirectory = Path.Combine(rootDirectory, parentDirectory);

            if (!Directory.Exists(fullDirectory))
            {
                throw new NotFoundException($"The directory '{fullDirectory}' does not exist");
            }

            return await Task.Run(() =>
            {
                var dir = new DirectoryInfo(fullDirectory);

                var contents = dir.GetFileSystemInfos("*", enumerationOptions);

                var files = contents
                    .OfType<FileInfo>()
                    .Select(f => FilesystemFile.FromFileInfo(f));

                var dirs = contents
                    .OfType<DirectoryInfo>()
                    .Prepend(dir)
                    .Select(d => FilesystemDirectory.FromDirectoryInfo(d) with
                    {
                        FullName = d.FullName.ReplaceFirst(rootDirectory, string.Empty).TrimStart('\\', '/'),
                        Files = files
                            .Where(f => Path.GetDirectoryName(f.FullName) == d.FullName)
                            .Select(f => f with { FullName = f.FullName.ReplaceFirst(rootDirectory, string.Empty).TrimStart('\\', '/') }),
                    });

                return dirs;
            });
        }
    }
}
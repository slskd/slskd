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
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using OneOf;

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

        private IEnumerable<string> AllowedDirectories => new[] { OptionsSnapshot.Value.Directories.Downloads, OptionsSnapshot.Value.Directories.Incomplete };
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

        /// <summary>
        ///     Deletes the specified <paramref name="directories"/>.
        /// </summary>
        /// <remarks>
        ///     Returns a dictionary keyed on directory name and containing a result for each specified directory. Exceptions are
        ///     contained in the result, and are not thrown.
        /// </remarks>
        /// <param name="rootDirectory">The root directory.</param>
        /// <param name="directories">The directories to delete.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="NotFoundException">Thrown if a specified directory does not exist.</exception>
        /// <exception cref="ForbiddenException">Thrown if a specified directory is restricted.</exception>
        public async Task<Dictionary<string, OneOf<bool, Exception>>> DeleteDirectoriesAsync(string rootDirectory, params string[] directories)
        {
            if (!AllowedDirectories.Any(d => rootDirectory == d))
            {
                throw new ForbiddenException($"For security reasons, only application-controlled directories can be deleted");
            }

            await Task.Yield();

            Dictionary<string, OneOf<bool, Exception>> results = new();

            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(Path.Combine(rootDirectory, directory), recursive: true);
                    results.Add(directory, true);
                }
                catch (FileNotFoundException)
                {
                    results.Add(directory, new NotFoundException($"The directory '{directory}' does not exist"));
                }
                catch (UnauthorizedAccessException)
                {
                    results.Add(directory, new ForbiddenException());
                }
                catch (Exception ex)
                {
                    results.Add(directory, ex);
                }
            }

            return results;
        }

        /// <summary>
        ///     Deletes the specified <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     Returns a dictionary keyed on directory name and containing a result for each specified directory. Exceptions are
        ///     contained in the result, and are not thrown.
        /// </remarks>
        /// <param name="rootDirectory">The root directory.</param>
        /// <param name="files">The list of files to delete.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="NotFoundException">Thrown if a specified file does not exist.</exception>
        /// <exception cref="ForbiddenException">Thrown if a specified file is restricted.</exception>
        public async Task<Dictionary<string, OneOf<bool, Exception>>> DeleteFilesAsync(string rootDirectory, params string[] files)
        {
            if (!AllowedDirectories.Any(d => rootDirectory == d))
            {
                throw new ForbiddenException($"For security reasons, only application-controlled directories can be deleted");
            }

            await Task.Yield();

            Dictionary<string, OneOf<bool, Exception>> results = new();

            foreach (var file in files)
            {
                try
                {
                    File.Delete(Path.Combine(rootDirectory, file));
                    results.Add(file, true);
                }
                catch (FileNotFoundException)
                {
                    results.Add(file, new NotFoundException($"The file '{file}' does not exist"));
                }
                catch (UnauthorizedAccessException)
                {
                    results.Add(file, new ForbiddenException());
                }
                catch (Exception ex)
                {
                    results.Add(file, ex);
                }
            }

            return results;
        }

        /// <summary>
        ///     Lists the contents in the specified <paramref name="parentDirectory"/>, optionally applying the specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="rootDirectory">The root directory.</param>
        /// <param name="parentDirectory">An optional subdirectory from which to start the listing.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found contents.</returns>
        /// <exception cref="NotFoundException">Thrown if the specified directory does not exist.</exception>
        /// <exception cref="ForbiddenException">Thrown if the specified directory is restricted.</exception>
        public async Task<FilesystemDirectory> ListContentsAsync(string rootDirectory, string parentDirectory = null, EnumerationOptions enumerationOptions = null)
        {
            if (!AllowedDirectories.Any(d => rootDirectory == d))
            {
                throw new ForbiddenException($"For security reasons, only application-controlled directories can be deleted");
            }

            var fullDirectory = parentDirectory is not null ? Path.Combine(rootDirectory, parentDirectory) : rootDirectory;

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
                    .Select(f => FilesystemFile.FromFileInfo(f) with
                    {
                        FullName = f.FullName.ReplaceFirst(rootDirectory, string.Empty).TrimStart('\\', '/'),
                    });

                var dirs = contents
                    .OfType<DirectoryInfo>()
                    .Select(d => FilesystemDirectory.FromDirectoryInfo(d) with
                    {
                        FullName = d.FullName.ReplaceFirst(rootDirectory, string.Empty).TrimStart('\\', '/'),
                    });

                var response = FilesystemDirectory.FromDirectoryInfo(dir) with
                {
                    FullName = dir.FullName.ReplaceFirst(rootDirectory, string.Empty).TrimStart('\\', '/'),
                    Files = files,
                    Directories = dirs,
                };

                return response;
            });
        }
    }
}
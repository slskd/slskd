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
    using Serilog;

    /// <summary>
    ///     Manages files on disk.
    /// </summary>
    public class FileService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FileService"/> class.
        /// </summary>
        public FileService(
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
        }

        private IEnumerable<string> AllowedDirectories => new[]
        {
            Path.GetFullPath(OptionsSnapshot.Value.Directories.Downloads),
            Path.GetFullPath(OptionsSnapshot.Value.Directories.Incomplete),
        };

        private ILogger Log { get; } = Serilog.Log.ForContext<FileService>();
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

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
        /// <exception cref="UnauthorizedException">Thrown if a specified directory is restricted.</exception>
        public virtual async Task<Dictionary<string, OneOf<bool, Exception>>> DeleteDirectoriesAsync(params string[] directories)
        {
            if (directories.Any(directory => Path.GetFullPath(directory) != directory))
            {
                throw new ArgumentException("Only absolute paths may be specified", nameof(directories));
            }

            if (directories.Any(directory => AllowedDirectories.Contains(directory)))
            {
                throw new ArgumentException("Deletion of application-controlled directory roots is not supported");
            }

            // important! we must fully expand the given paths with GetFullPath() to resolve a given relative directory, like '..'
            bool IsAllowed(string path) => AllowedDirectories.Any(allowed => path.StartsWith(allowed));

            // if any of the resolved directory paths aren't rooted in one of the allowed directories, forbid the entire request
            if (!directories.All(directory => IsAllowed(directory)))
            {
                throw new UnauthorizedException("Only application-controlled directories can be deleted");
            }

            await Task.Yield();

            Dictionary<string, OneOf<bool, Exception>> results = new();

            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    results.Add(directory, true);

                    Log.Information("Deleted directory '{Directory}'", directory);
                }
                catch (DirectoryNotFoundException)
                {
                    results.Add(directory, new NotFoundException($"The directory '{directory}' does not exist"));
                }
                catch (UnauthorizedAccessException ex)
                {
                    results.Add(directory, new UnauthorizedException($"Deletion of the directory '{directory}' was denied: {ex.Message}", ex));
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
        /// <param name="files">The list of files to delete.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the specified files have a relative path.</exception>
        /// <exception cref="NotFoundException">Thrown if a specified file does not exist.</exception>
        /// <exception cref="UnauthorizedException">Thrown if a specified file is restricted.</exception>
        public virtual async Task<Dictionary<string, OneOf<bool, Exception>>> DeleteFilesAsync(params string[] files)
        {
            if (files.Any(file => Path.GetFullPath(file) != file))
            {
                throw new ArgumentException("Only absolute paths may be specified", nameof(files));
            }

            // important! we must fully expand the given paths with GetFullPath() to resolve a given relative directory, like '..'
            bool IsAllowed(string path) => AllowedDirectories.Any(allowed => path.StartsWith(allowed));

            // if any of the resolved file paths aren't rooted in one of the allowed directories, forbid the entire request
            if (!files.All(file => IsAllowed(file)))
            {
                throw new UnauthorizedException("Only files in application-controlled directories can be deleted");
            }

            await Task.Yield();

            Dictionary<string, OneOf<bool, Exception>> results = new();

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    results.Add(file, true);
                }
                catch (FileNotFoundException)
                {
                    results.Add(file, new NotFoundException($"The file '{file}' does not exist"));
                }
                catch (UnauthorizedAccessException ex)
                {
                    results.Add(file, new UnauthorizedException($"Deletion of the file '{file}' was denied: {ex.Message}", ex));
                }
                catch (Exception ex)
                {
                    results.Add(file, ex);
                }
            }

            return results;
        }

        /// <summary>
        ///     Lists the contents in the specified <paramref name="directory"/>, optionally applying the specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="directory">The directory from which to start the listing.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found contents.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified directory has a relative path.</exception>
        /// <exception cref="NotFoundException">Thrown if the specified directory does not exist.</exception>
        /// <exception cref="UnauthorizedException">Thrown if the specified root directory is restricted.</exception>
        public virtual async Task<FilesystemDirectory> ListContentsAsync(string directory, EnumerationOptions enumerationOptions = null)
        {
            if (Path.GetFullPath(directory) != directory)
            {
                throw new ArgumentException("Only absolute paths may be specified", nameof(directory));
            }

            // important! we must fully expand the path with GetFullPath() to resolve a given relative directory, like '..'
            if (!AllowedDirectories.Any(allowed => directory.StartsWith(allowed)))
            {
                throw new UnauthorizedException($"Only application-controlled directories can be deleted");
            }

            if (!Directory.Exists(directory))
            {
                throw new NotFoundException($"The directory '{directory}' does not exist");
            }

            return await Task.Run(() =>
            {
                var dir = new DirectoryInfo(directory);

                try
                {
                    var contents = dir.GetFileSystemInfos("*", enumerationOptions);

                    var files = contents
                        .OfType<FileInfo>()
                        .Select(f => FilesystemFile.FromFileInfo(f) with
                        {
                            FullName = f.FullName.ReplaceFirst(directory, string.Empty).TrimStart('\\', '/'),
                        });

                    var dirs = contents
                        .OfType<DirectoryInfo>()
                        .Select(d => FilesystemDirectory.FromDirectoryInfo(d) with
                        {
                            FullName = d.FullName.ReplaceFirst(directory, string.Empty).TrimStart('\\', '/'),
                        });

                    var response = FilesystemDirectory.FromDirectoryInfo(dir) with
                    {
                        FullName = dir.FullName.ReplaceFirst(directory, string.Empty).TrimStart('\\', '/'),
                        Files = files,
                        Directories = dirs,
                    };

                    return response;
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new UnauthorizedException($"Access to directory '{directory}' was denied: {ex.Message}", ex);
                }
            });
        }
    }
}
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
            IOptionsMonitor<Options> optionsMonitor)
        {
            OptionsMonitor = optionsMonitor;
        }

        private IEnumerable<string> AllowedDirectories => new[]
        {
            Path.GetFullPath(OptionsMonitor.CurrentValue.Directories.Downloads),
            Path.GetFullPath(OptionsMonitor.CurrentValue.Directories.Incomplete),
        };

        private ILogger Log { get; } = Serilog.Log.ForContext<FileService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }

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

        /// <summary>
        ///     Creates a new file with the specified fully qualified <paramref name="filename"/> and the optional <paramref name="options"/>,
        ///     returning a <see cref="Stream"/> with which the contents of the file can be written.
        /// </summary>
        /// <remarks>
        ///     Reasonable defaults, including the Unix permissions from app configuration, have been applied. Be sure to review the defaults
        ///     for each new use case and ensure they are appropriate.
        /// </remarks>
        /// <param name="filename">The fully qualified filename.</param>
        /// <param name="options">An optional patch for the underlying <see cref="FileStreamOptions"/>.</param>
        /// <returns>A Stream with which the contents of the file can be written.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified filename is null or contains only whitespace.</exception>
        /// <exception cref="IOException">Thrown if the underlying file or Stream can't be created for some reason.</exception>
        public virtual Stream CreateFile(string filename, CreateFileOptions options = null)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filename, nameof(filename));

            var path = Path.GetDirectoryName(filename);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var streamOptions = new FileStreamOptions
            {
                Access = options?.Access ?? FileAccess.Write,
                BufferSize = options?.BufferSize ?? 4096, // framework default
                Mode = options?.Mode ?? FileMode.Create, // overwrite
                Options = options?.Options ?? FileOptions.None, // synchronous I/O
                PreallocationSize = options?.PreallocationSize ?? 0,
                Share = options?.Share ?? FileShare.None, // exclusive access
            };

            // attempting to use UnixCreateMode on Windows raises an Exception
            // we *MUST* check the OS and skip this on Windows
            if (!OperatingSystem.IsWindows())
            {
                var appOption = OptionsMonitor.CurrentValue.Permissions.File.Mode;

                // if options haven't been passed in and none have been set in app config, omit this
                // so that the application's umask will be applied, saving users the hassle of setting it twice.
                if (options?.UnixCreateMode != null || !string.IsNullOrWhiteSpace(appOption))
                {
                    streamOptions.UnixCreateMode = options?.UnixCreateMode ?? appOption?.ToUnixFileMode();
                    Log.Debug("Setting Unix file mode to {Mode}", streamOptions.UnixCreateMode);
                }
            }

            try
            {
                return new FileStream(filename, streamOptions);
            }
            catch (Exception ex)
            {
                // the operation above can throw quite a few exceptions, all granular variations of
                // IOException. to make handling downstream easier, wrap them all up and re-throw.
                throw new IOException($"Failed to create file {Path.GetFileName(filename)}: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Moves the specified fully qualified, localized, <paramref name="sourceFilename"/> to the specified fully qualified, localized,
        ///     <paramref name="destinationDirectory"/>.
        /// </summary>
        /// <remarks>
        ///     If the destination file already exists and the <paramref name="overwrite"/> option is not set, the destination filename will
        ///     be modified to include the current time to avoid the collision while preserving both files.
        /// </remarks>
        /// <param name="sourceFilename">The fully qualified filename of the file to move.</param>
        /// <param name="destinationDirectory">The fully qualified directory to which to move the source file.</param>
        /// <param name="overwrite">An optional value indicating whether the destination file should be overwritten if it already exists.</param>
        /// <param name="deleteSourceDirectoryIfEmptyAfterMove">
        ///     An optional value indicating whether the parent directory of the source file should be deleted if it is empty after the move.
        /// </param>
        /// <returns>The fully qualified filename of the resulting file.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either of the specified file or directories are null or contain only whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified <paramref name="sourceFilename"/> does not exist.</exception>
        /// <exception cref="IOException">Thrown if the file can't be moved, or the <paramref name="deleteSourceDirectoryIfEmptyAfterMove"/> option is set and the operation fails.</exception>
        public virtual string MoveFile(string sourceFilename, string destinationDirectory, bool overwrite = false, bool deleteSourceDirectoryIfEmptyAfterMove = false)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(sourceFilename, nameof(sourceFilename));
            ArgumentNullException.ThrowIfNullOrWhiteSpace(destinationDirectory, nameof(destinationDirectory));

            if (!File.Exists(sourceFilename))
            {
                throw new FileNotFoundException($"The specified source file does not exist", fileName: sourceFilename);
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var destinationFilename = Path.Combine(destinationDirectory, Path.GetFileName(sourceFilename));

            Log.Debug("Attempting to move {Source} to {Destination}", sourceFilename, destinationFilename);

            if (!overwrite && File.Exists(destinationFilename))
            {
                Log.Debug("Destination file {Destination} exists, and overwite option is not set; attempting to generate new filename", destinationFilename);

                string extensionlessFilename = Path.Combine(Path.GetDirectoryName(destinationFilename), Path.GetFileNameWithoutExtension(sourceFilename));
                string extension = Path.GetExtension(sourceFilename);

                while (File.Exists(destinationFilename))
                {
                    destinationFilename = $"{extensionlessFilename}_{DateTime.UtcNow.Ticks}{extension}";
                }
            }

            try
            {
                File.Move(sourceFilename, destinationFilename, overwrite: overwrite);

                Log.Debug("Successfully moved {Source} to {Destination}", sourceFilename, destinationFilename);

                // if the parent directory is empty after the move, delete it
                if (deleteSourceDirectoryIfEmptyAfterMove && !Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(sourceFilename)).Any())
                {
                    Directory.Delete(Path.GetDirectoryName(sourceFilename));
                }

                return destinationFilename;
            }
            catch (Exception ex)
            {
                // the operation above can throw quite a few exceptions, all granular variations of
                // IOException. to make handling downstream easier, wrap them all up and re-throw.
                throw new IOException($"Failed to move file {Path.GetFileName(sourceFilename)}: {ex.Message}", ex);
            }
        }
    }
}
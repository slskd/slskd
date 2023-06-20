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
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    /// <summary>
    ///     Manages files on disk.
    /// </summary>
    public class FileService : IFileService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FileService"/> class.
        /// </summary>
        /// <param name="optionsSnapshot"></param>
        public FileService(
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
        }

        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

        /// <summary>
        ///     Recursively lists all of the files in the downloads directory, starting from the optional <paramref name="parentDirectory"/>, and
        ///     optionally applying the specified <paramref name="enumerationOptions"/>.
        /// </summary>
        /// <param name="parentDirectory">An optional parent directory from which to begin searching.</param>
        /// <param name="enumerationOptions">Optional enumeration options to apply.</param>
        /// <returns>The list of found files.</returns>
        public async Task<IEnumerable<FileInfo>> ListDownloadedFiles(string parentDirectory = null, EnumerationOptions enumerationOptions = null)
        {
            var root = OptionsSnapshot.Value.Directories.Downloads;
            parentDirectory = Path.Combine(root, parentDirectory) ?? root;
            var dir = new DirectoryInfo(parentDirectory);

            enumerationOptions ??= new EnumerationOptions();

            var files = await Task.Run(() =>
            {
                return dir.GetFiles("*", enumerationOptions);
            });

            return files.AsEnumerable();
        }

        public Task<IEnumerable<FileInfo>> ListIncompleteFiles(Expression<Func<FileInfo, bool>> expression = null)
        {
            throw new NotImplementedException();
        }
    }
}

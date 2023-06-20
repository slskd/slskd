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

        public async Task<IEnumerable<FileInfo>> ListDownloadedFiles()
        {
            var dir = new DirectoryInfo(OptionsSnapshot.Value.Directories.Downloads);

            var files = await Task.Run(() =>
            {
                return dir.GetFiles("*", new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = false,
                    RecurseSubdirectories = true,
                });
            });

            return files.AsEnumerable();
        }

        public Task<IEnumerable<FileInfo>> ListIncompleteFiles(Expression<Func<FileInfo, bool>> expression = null)
        {
            throw new NotImplementedException();
        }
    }
}

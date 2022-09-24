// <copyright file="ISharedFileCache.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Soulseek;

    /// <summary>
    ///     Shared file cache.
    /// </summary>
    public interface ISharedFileCache
    {
        /// <summary>
        ///     Gets the cache state monitor.
        /// </summary>
        IStateMonitor<SharedFileCacheState> StateMonitor { get; }

        /// <summary>
        ///     Returns the contents of the cache.
        /// </summary>
        /// <param name="share">The optional share to which to limit the scope of the browse.</param>
        /// <returns>The contents of the cache.</returns>
        IEnumerable<Directory> Browse(Share share = null);

        /// <summary>
        ///     Returns the number of directories in the specified <paramref name="share"/>.
        /// </summary>
        /// <param name="share">The share for which the directories are to be counted.</param>
        /// <returns>The number of directories.</returns>
        int CountDirectories(Share share);

        /// <summary>
        ///     Returns the number of files in the specified <paramref name="share"/>.
        /// </summary>
        /// <param name="share">The share for which the files are to be counted.</param>
        /// <returns>The number of files.</returns>
        int CountFiles(Share share);

        /// <summary>
        ///     Scans the configured shares and fills the cache.
        /// </summary>
        /// <param name="shares">The list of shares from which to fill the cache.</param>
        /// <param name="filters">The list of regular expressions used to exclude files or paths from scanning.</param>
        /// <returns>The operation context.</returns>
        Task FillAsync(IEnumerable<Share> shares, IEnumerable<Regex> filters);

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        Directory List(string directory);

        /// <summary>
        ///     Substitutes the mask in the specified <paramref name="filename"/> with the original path, if the mask is tracked
        ///     by the cache.
        /// </summary>
        /// <param name="filename">The fully qualified filename to unmask.</param>
        /// <returns>The unmasked filename.</returns>
        string Resolve(string filename);

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        IEnumerable<File> Search(SearchQuery query);

        /// <summary>
        ///     Cancels the currently running fill operation, if one is running.
        /// </summary>
        /// <returns>A value indicating whether a fill operation was cancelled.</returns>
        bool TryCancelFill();

        /// <summary>
        ///     Attempts to load the cache from disk.
        /// </summary>
        /// <returns>A value indicating whether the load was successful.</returns>
        bool TryLoad();
    }
}
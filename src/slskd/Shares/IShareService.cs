// <copyright file="IShareService.cs" company="slskd Team">
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
    using System.Threading.Tasks;
    using Soulseek;

    /// <summary>
    ///     Provides control and interactions with configured shares and shared files.
    /// </summary>
    public interface IShareService
    {
        /// <summary>
        ///     Gets the list of configured shares.
        /// </summary>
        IReadOnlyList<Share> Shares { get; }

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        public IStateMonitor<ShareState> StateMonitor { get; }

        /// <summary>
        ///     Returns the entire contents of the share.
        /// </summary>
        /// <returns>The entire contents of the share.</returns>
        Task<IEnumerable<Directory>> BrowseAsync();

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        Task<Directory> ListDirectoryAsync(string directory);

        /// <summary>
        ///     Resolves the local filename of the specified <paramref name="remoteFilename"/>, if the mask is associated with a
        ///     configured share.
        /// </summary>
        /// <param name="remoteFilename">The fully qualified filename to resolve.</param>
        /// <returns>The resolved local filename.</returns>
        /// <exception cref="NotFoundException">
        ///     Thrown when the specified remote filename can not be associated with a configured share.
        /// </exception>
        Task<string> ResolveFilenameAsync(string remoteFilename);

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        Task<IEnumerable<File>> SearchAsync(SearchQuery query);

        /// <summary>
        ///     Starts a scan of the configured shares.
        /// </summary>
        /// <returns>The operation context.</returns>
        /// <exception cref="ShareScanInProgressException">Thrown when a scan is already in progress.</exception>
        Task StartScanAsync();
    }
}

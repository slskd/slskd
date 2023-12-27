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
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Soulseek;

    /// <summary>
    ///     Provides control and interactions with configured shares and shared files.
    /// </summary>
    public interface IShareService
    {
        /// <summary>
        ///     Gets the list of share hosts.
        /// </summary>
        IReadOnlyList<Host> Hosts { get; }

        /// <summary>
        ///     Gets the local share host.
        /// </summary>
        Host LocalHost { get; }

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        IStateMonitor<ShareState> StateMonitor { get; }

        /// <summary>
        ///     Adds a new, or updates an existing, share host.
        /// </summary>
        /// <param name="host">The host to add or update.</param>
        void AddOrUpdateHost(Host host);

        /// <summary>
        ///     Returns the entire contents of the share.
        /// </summary>
        /// <returns>The entire contents of the share.</returns>
        Task<IEnumerable<Directory>> BrowseAsync(Share share = null);

        /// <summary>
        ///     Dumps the local share cache to a file.
        /// </summary>
        /// <param name="filename">The destination file.</param>
        /// <returns>The operation context.</returns>
        Task DumpAsync(string filename);

        /// <summary>
        ///     Initializes the service and shares.
        /// </summary>
        /// <param name="forceRescan">A value indicating whether a full re-scan of shares should be performed.</param>
        /// <returns>The operation context.</returns>
        Task InitializeAsync(bool forceRescan = false);

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        Task<Directory> ListDirectoryAsync(string directory);

        /// <summary>
        ///     Returns the list of all <see cref="Scan"/> records matching the specified <paramref name="predicate"/>.
        /// </summary>
        /// <param name="predicate">An optional expression used to locate scans.</param>
        /// <returns>The operation context, including the list of found scans.</returns>
        Task<IEnumerable<Scan>> ListScansAsync(Expression<Func<Scan, bool>> predicate = null);

        /// <summary>
        ///     Requests that a share scan is performed.
        /// </summary>
        void RequestScan();

        /// <summary>
        ///     Resolves the local filename of the specified <paramref name="remoteFilename"/>, if the mask is associated with a
        ///     configured share.
        /// </summary>
        /// <param name="remoteFilename">The fully qualified filename to resolve.</param>
        /// <returns>The resolved host and filename.</returns>
        /// <exception cref="NotFoundException">
        ///     Thrown when the specified remote filename can not be associated with a configured share.
        /// </exception>
        Task<(string Host, string Filename)> ResolveFileAsync(string remoteFilename);

        /// <summary>
        ///     Scans the configured shares on the local host.
        /// </summary>
        /// <returns>The operation context.</returns>
        /// <exception cref="ShareScanInProgressException">Thrown when a scan is already in progress.</exception>
        Task ScanAsync();

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        Task<IEnumerable<File>> SearchAsync(SearchQuery query);

        /// <summary>
        ///     Cancels the currently running scan on the local host, if one is running.
        /// </summary>
        /// <returns>A value indicating whether a scan was cancelled.</returns>
        bool TryCancelScan();

        /// <summary>
        ///     Returns the share host with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <param name="host">The host, if found.</param>
        /// <returns>A value indicating whether the host was found.</returns>
        bool TryGetHost(string name, out Host host);

        /// <summary>
        ///     Removes the share host with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <returns>A value indicating whether the host was removed.</returns>
        bool TryRemoveHost(string name);
    }
}
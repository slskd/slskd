// <copyright file="ShareService.cs" company="slskd Team">
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

using System.IO;
using Microsoft.Extensions.Options;

namespace slskd.Shares
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Soulseek;

    /// <summary>
    ///     Provides control and interactions with configured shares and shared files.
    /// </summary>
    public class ShareService : IShareService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ShareService"/> class.
        /// </summary>
        /// <param name="optionsMonitor"></param>
        /// <param name="sharedFileCache"></param>
        public ShareService(
            IOptionsMonitor<Options> optionsMonitor,
            ISharedFileCache sharedFileCache = null)
        {
            Cache = sharedFileCache ?? new SharedFileCache(
                storageMode: optionsMonitor.CurrentValue.Shares.Cache.StorageMode.ToEnum<StorageMode>(),
                workerCount: optionsMonitor.CurrentValue.Shares.Cache.Workers);

            Cache.StateMonitor.OnChange(cacheState =>
            {
                var (previous, current) = cacheState;

                State.SetValue(state => state with
                {
                    // scan is pending if faulted, or if state DIDN'T just transition from filling to not filling AND a scan was already pending
                    ScanPending = current.Faulted || current.Cancelled || (!(previous.Filling && !current.Filling) && state.ScanPending),
                    Scanning = current.Filling,
                    Faulted = current.Faulted,
                    Cancelled = current.Cancelled,
                    Ready = current.Filled,
                    ScanProgress = current.FillProgress,
                    Directories = current.Directories,
                    Files = current.Files,
                });
            });

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the list of configured shares.
        /// </summary>
        public IReadOnlyList<Share> Shares => SharesList.AsReadOnly();

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        public IStateMonitor<ShareState> StateMonitor => State;

        private ISharedFileCache Cache { get; }
        private string LastOptionsHash { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private List<Share> SharesList { get; set; } = new List<Share>();
        private IManagedState<ShareState> State { get; } = new ManagedState<ShareState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private List<Regex> FilterRegexes { get; set; } = new List<Regex>();

        /// <summary>
        ///     Returns the entire contents of the share.
        /// </summary>
        /// <returns>The entire contents of the share.</returns>
        public Task<IEnumerable<Directory>> BrowseAsync(Share share = null)
        {
            var results = Cache.Browse(share);
            var normalizedResults = results.Select(r => new Directory(r.Name.NormalizePath(), r.Files));

            return Task.FromResult(normalizedResults);
        }

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        public Task<Directory> ListDirectoryAsync(string directory)
        {
            var list = Cache.List(directory.LocalizePath());
            var normalizedList = new Directory(list.Name.NormalizePath(), list.Files);

            return Task.FromResult(normalizedList);
        }

        /// <summary>
        ///     Resolves the local filename of the specified <paramref name="remoteFilename"/>, if the mask is associated with a
        ///     configured share.
        /// </summary>
        /// <param name="remoteFilename">The fully qualified filename to resolve.</param>
        /// <returns>The resolved local filename.</returns>
        /// <exception cref="NotFoundException">
        ///     Thrown when the specified remote filename can not be associated with a configured share.
        /// </exception>
        public Task<FileInfo> ResolveFileAsync(string remoteFilename)
        {
            var resolvedFilename = Cache.Resolve(remoteFilename.LocalizePath());

            if (string.IsNullOrEmpty(resolvedFilename))
            {
                throw new NotFoundException($"The requested filename '{remoteFilename}' could not be resolved to a local file");
            }

            var fileInfo = new FileInfo(resolvedFilename);

            if (!fileInfo.Exists)
            {
                // the shared file cache has divered from the physical filesystem; the user needs to perform a scan to reconcile.
                State.SetValue(state => state with { ScanPending = true });
                throw new NotFoundException($"The resolved file '{resolvedFilename}' could not be located on disk. A share scan should be performed.");
            }

            return Task.FromResult(fileInfo);
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public Task<IEnumerable<File>> SearchAsync(SearchQuery query)
        {
            var results = Cache.Search(query);

            return Task.FromResult(results.Select(r => new File(
                r.Code,
                r.Filename.NormalizePath(),
                r.Size,
                r.Extension,
                r.Attributes)));
        }

        /// <summary>
        ///     Scans the configured shares.
        /// </summary>
        /// <returns>The operation context.</returns>
        /// <exception cref="ShareScanInProgressException">Thrown when a scan is already in progress.</exception>
        public Task ScanAsync()
        {
            return Cache.FillAsync(Shares, FilterRegexes);
        }

        /// <summary>
        ///     Gets summary information for the specified <paramref name="share"/>.
        /// </summary>
        /// <param name="share">The share to summarize.</param>
        /// <returns>The summary information.</returns>
        public Task<(int Directories, int Files)> SummarizeShareAsync(Share share)
        {
            var dirs = Cache.CountDirectories(share);
            var files = Cache.CountFiles(share);
            return Task.FromResult((dirs, files));
        }

        /// <summary>
        ///     Cancels the currently running scan, if one is running.
        /// </summary>
        /// <returns>A value indicating whether a scan was cancelled.</returns>
        public bool TryCancelScan()
        {
            return Cache.TryCancelFill();
        }

        /// <summary>
        ///     Attempt to load shares from disk.
        /// </summary>
        /// <returns>A value indicating whether shares were loaded.</returns>
        public Task<bool> TryLoadFromDiskAsync()
        {
            return Task.FromResult(Cache.TryLoad());
        }

        private void Configure(Options options)
        {
            SyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(string.Join(';', options.Shares.Directories));

                if (optionsHash == LastOptionsHash)
                {
                    return;
                }

                var shares = options.Shares.Directories
                    .Select(share => share.TrimEnd('/', '\\'))
                    .ToHashSet() // remove duplicates
                    .Select(share => new Share(share)) // convert to Shares
                    .OrderByDescending(share => share.LocalPath.Length) // process subdirectories first.  this allows them to be aliased separately from their parent
                    .ToList();

                var regexes = options.Shares.Filters
                    .Select(filter => new Regex(filter, RegexOptions.Compiled))
                    .ToList();

                SharesList = shares;
                FilterRegexes = regexes;

                State.SetValue(state => state with { ScanPending = true });

                LastOptionsHash = optionsHash;
            }
            finally
            {
                SyncRoot.Release();
            }
        }
    }
}
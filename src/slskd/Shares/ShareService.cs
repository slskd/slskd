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
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
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
        /// <param name="scanner"></param>
        public ShareService(
            IOptionsMonitor<Options> optionsMonitor,
            IShareScanner scanner = null)
        {
            CacheStorageMode = optionsMonitor.CurrentValue.Shares.Cache.StorageMode.ToEnum<StorageMode>();

            Scanner = scanner ?? new ShareScanner(
                workerCount: optionsMonitor.CurrentValue.Shares.Cache.Workers);

            Scanner.StateMonitor.OnChange(cacheState =>
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

            Repository = new ShareRepository(connectionString: Program.ConnectionStrings.Shares);

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            StateMonitor = State;

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the list of configured shares.
        /// </summary>
        public IReadOnlyList<Share> Shares => SharesList.AsReadOnly();

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        public IStateMonitor<ShareState> StateMonitor { get; }

        private IShareScanner Scanner { get; }
        private IShareRepository Repository { get; }
        private string LastOptionsHash { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private List<Share> SharesList { get; set; } = new List<Share>();
        private IManagedState<ShareState> State { get; } = new ManagedState<ShareState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private List<Regex> FilterRegexes { get; set; } = new List<Regex>();
        private StorageMode CacheStorageMode { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ShareService>();

        /// <summary>
        ///     Backs contents of the cache up to the specified file.
        /// </summary>
        /// <param name="connectionString">The destination connection string.</param>
        public void BackupTo(string connectionString)
        {
            Repository.BackupTo(connectionString);
        }

        /// <summary>
        ///     Returns the entire contents of the share.
        /// </summary>
        /// <returns>The entire contents of the share.</returns>
        public Task<IEnumerable<Directory>> BrowseAsync(Share share = null)
        {
            var directories = new ConcurrentDictionary<string, Directory>();

            string prefix = null;

            if (share != null)
            {
                prefix = share.RemotePath + Path.DirectorySeparatorChar;
            }

            // Soulseek requires that each directory in the tree have an entry in the list returned in a browse response. if
            // missing, files that are nested within directories which contain only directories (no files) are displayed as being
            // in the root. to get around this, prime a dictionary with all known directories, and an empty Soulseek.Directory. if
            // there are any files in the directory, this entry will be overwritten with a new Soulseek.Directory containing the
            // files. if not they'll be left as is.
            foreach (var directory in Repository.ListDirectories(prefix))
            {
                var name = directory.NormalizePathForSoulseek();
                directories.TryAdd(name, new Directory(name));
            }

            var files = Repository.ListFiles(prefix, includeFullPath: true);

            var groups = files
                .GroupBy(file => Path.GetDirectoryName(file.Filename))
                .Select(group => new Directory(group.Key.NormalizePathForSoulseek(), group.Select(f =>
                {
                    return new File(
                        f.Code,
                        Path.GetFileName(f.Filename), // we can send the full path, or just the filename.  save bandwidth and omit the path.
                        f.Size,
                        f.Extension,
                        f.Attributes);
                }).OrderBy(f => f.Filename)));

            // merge the dictionary containing all directories with the Soulseek.Directory instances containing their files.
            // entries with no files will remain untouched.
            foreach (var group in groups)
            {
                directories.AddOrUpdate(group.Name, group, (_, _) => group);
            }

            var results = directories.Values.AsEnumerable();

            return Task.FromResult(results);
        }

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        public Task<Directory> ListDirectoryAsync(string directory)
        {
            var localPath = directory.LocalizePath();

            var files = Repository.ListFiles(localPath);

            return Task.FromResult(new Directory(directory, files));
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
        public Task<string> ResolveFileAsync(string remoteFilename)
        {
            var resolvedFilename = Repository.FindFilename(remoteFilename.LocalizePath());

            if (string.IsNullOrEmpty(resolvedFilename))
            {
                throw new NotFoundException($"The requested filename '{remoteFilename}' could not be resolved to a local file");
            }

            return Task.FromResult(resolvedFilename);
        }

        /// <summary>
        ///     Requests that a share scan is performed.
        /// </summary>
        public void RequestScan()
        {
            State.SetValue(state => state with { ScanPending = true });
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public Task<IEnumerable<File>> SearchAsync(SearchQuery query)
        {
            var results = Repository.Search(query);

            return Task.FromResult(results.Select(r => new File(
                r.Code,
                r.Filename.NormalizePathForSoulseek(),
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
            return Scanner.ScanAsync(Shares, FilterRegexes);
        }

        /// <summary>
        ///     Gets summary information for the specified <paramref name="share"/>.
        /// </summary>
        /// <param name="share">The share to summarize.</param>
        /// <returns>The summary information.</returns>
        public Task<(int Directories, int Files)> SummarizeShareAsync(Share share)
        {
            var prefix = share.RemotePath + Path.DirectorySeparatorChar;

            var dirs = Repository.CountDirectories(prefix);
            var files = Repository.CountFiles(prefix);
            return Task.FromResult((dirs, files));
        }

        /// <summary>
        ///     Cancels the currently running scan, if one is running.
        /// </summary>
        /// <returns>A value indicating whether a scan was cancelled.</returns>
        public bool TryCancelScan()
        {
            return Scanner.TryCancelScan();
        }

        /// <summary>
        ///     Initializes the service and shares.
        /// </summary>
        /// <param name="forceRescan">A value indicating whether a full re-scan of shares should be performed.</param>
        /// <returns>The operation context.</returns>
        public async Task InitializeAsync(bool forceRescan = false)
        {
            try
            {
                if (forceRescan)
                {
                    Log.Warning("Performing a forced re-scan of shares");
                    await ScanAsync();
                }
                else if (CacheStorageMode == StorageMode.Memory || (CacheStorageMode == StorageMode.Disk && !ShareRepository.TryValidateDatabase(Program.ConnectionStrings.Shares)))
                {
                    Log.Debug($"Share cache {(CacheStorageMode == StorageMode.Memory ? "StorageMode is 'Memory'" : "database is missing from disk")}. Attempting to load from backup...");

                    // the backup is missing; we can't do anything but recreate it from scratch
                    if (!ShareRepository.TryValidateDatabase(Program.ConnectionStrings.SharesBackup))
                    {
                        Log.Warning("Share cache couldn't be loaded from backup");
                        await InitializeAsync(forceRescan: true);
                    }
                    else
                    {
                        Log.Debug("Share cache backup located. Attempting to restore...");

                        Repository.RestoreFrom(connectionString: Program.ConnectionStrings.SharesBackup);

                        Log.Information("Share cache successfully restored from backup");
                    }
                }
                else
                {
                    // the cache mode is disk and the existing db is valid. nothing to do.
                }

                // one of several thigns happened above before we got here:
                //   the storage mode is memory, and we loaded the in-memory db from a valid backup
                //   the storage mode is disk, and the file is there and valid
                //   the storage mode is disk but either missing or invalid, and we restored from a valid backup
                //   we failed to load the cache from disk or backup, and performed a full scan successfully
                // at this point there is a valid (existing, schema matching expected schema) database at the primary connection string
                State.SetValue(state => state with
                {
                    Scanning = false,
                    Faulted = false,
                    Ready = true,
                    ScanProgress = 1,
                    Directories = Repository.CountDirectories(),
                    Files = Repository.CountFiles(),
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error initializing shares: {Message}", ex.Message);

                if (!forceRescan)
                {
                    Log.Warning("Re-attempting initializtion");
                    await InitializeAsync(forceRescan: true);
                }
                else
                {
                    Log.Error("Failed to initialize shares, and an attempt to force a full scan to repair failed");
                    throw;
                }
            }
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
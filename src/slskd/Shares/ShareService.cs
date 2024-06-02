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

using Microsoft.Extensions.Options;

namespace slskd.Shares
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Relay;
    using Soulseek;

    /// <summary>
    ///     Provides control and interactions with configured shares and shared files.
    /// </summary>
    public class ShareService : IShareService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ShareService"/> class.
        /// </summary>
        /// <param name="shareRepositoryFactory"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="scanner"></param>
        public ShareService(
            IShareRepositoryFactory shareRepositoryFactory,
            IOptionsMonitor<Options> optionsMonitor,
            IShareScanner scanner = null)
        {
            var options = optionsMonitor.CurrentValue;

            CacheStorageMode = options.Shares.Cache.StorageMode.ToEnum<StorageMode>();

            ShareRepositoryFactory = shareRepositoryFactory;

            var host = new Host(Program.LocalHostName);
            var repository = ShareRepositoryFactory.CreateFromHost(host.Name);

            Local = (host, repository);

            AllRepositories = new List<IShareRepository>(new[] { repository });

            Scanner = scanner ?? new ShareScanner(workerCount: options.Shares.Cache.Workers);

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
                    ScanProgress = current.FillProgress,
                    Directories = current.Directories,
                    Files = current.Files,
                });
            });

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            StateMonitor = State;

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the list of share hosts.
        /// </summary>
        public IReadOnlyList<Host> Hosts => HostDictionary.Values.Select(v => v.Host).Prepend(Local.Host).ToList().AsReadOnly();

        /// <summary>
        ///     Gets the local share host.
        /// </summary>
        public Host LocalHost => Local.Host;

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        public IStateMonitor<ShareState> StateMonitor { get; }

        private IShareRepositoryFactory ShareRepositoryFactory { get; }
        private IShareScanner Scanner { get; }
        private SemaphoreSlim ScannerSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private string LastOptionsHash { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ConcurrentDictionary<string, (Host Host, IShareRepository Repository)> HostDictionary { get; set; } = new();
        private IManagedState<ShareState> State { get; } = new ManagedState<ShareState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private StorageMode CacheStorageMode { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ShareService>();
        private (Host Host, IShareRepository Repository) Local { get; set; }
        private IEnumerable<IShareRepository> AllRepositories { get; set; }

        /// <summary>
        ///     Adds a new, or updates an existing, share host.
        /// </summary>
        /// <param name="host">The host to add or update.</param>
        public void AddOrUpdateHost(Host host)
        {
            // we assume that if this method is called that the caller has verified that the
            // file for the host exists and is valid.
            HostDictionary.AddOrUpdate(
                key: host.Name,
                addValueFactory: (key) => (host, ShareRepositoryFactory.CreateFromHost(host.Name)),
                updateValueFactory: (_, existing) => (host, existing.Repository));

            AllRepositories = HostDictionary.Values
                .Select(value => value.Repository)
                .Prepend(Local.Repository);

            State.SetValue(state => state with
            {
                Hosts = Hosts.Select(host => host.Name).ToArray(),
                Directories = Hosts.SelectMany(host => host.Shares).Sum(share => share.Directories ?? 0),
                Files = Hosts.SelectMany(host => host.Shares).Sum(share => share.Files ?? 0),
            });
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
                prefix = share.RemotePath + (share.RemotePath.EndsWith('\\') ? string.Empty : '\\');
            }

            // Soulseek requires that each directory in the tree have an entry in the list returned in a browse response. if
            // missing, files that are nested within directories which contain only directories (no files) are displayed as being
            // in the root. to get around this, prime a dictionary with all known directories, and an empty Soulseek.Directory. if
            // there are any files in the directory, this entry will be overwritten with a new Soulseek.Directory containing the
            // files. if not they'll be left as is.
            foreach (var directory in AllRepositories.SelectMany(r => r.ListDirectories(prefix)))
            {
                directories.TryAdd(directory, new Directory(directory));
            }

            var files = AllRepositories.SelectMany(r => r.ListFiles(prefix, includeFullPath: true));

            var groups = files
                .GroupBy(file => file.Filename.GetNormalizedDirectoryName())
                .Select(group => new Directory(group.Key, group.Select(f =>
                {
                    return new File(
                        f.Code,
                        f.Filename.GetNormalizedFileName(), // we can send the full path, or just the filename.  save bandwidth and omit the path.
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
        ///     Dumps the local share cache to a file.
        /// </summary>
        /// <param name="filename">The destination file.</param>
        /// <returns>The operation context.</returns>
        public Task DumpAsync(string filename)
        {
            Local.Repository.DumpTo(filename);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Returns the share host with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <param name="host">The host, if found.</param>
        /// <returns>A value indicating whether the host was found.</returns>
        public bool TryGetHost(string name, out Host host)
        {
            if (HostDictionary.TryGetValue(name, out var record))
            {
                host = record.Host;
                return true;
            }

            host = null;
            return false;
        }

        /// <summary>
        ///     Removes the share host with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <returns>A value indicating whether the host was removed.</returns>
        public bool TryRemoveHost(string name)
        {
            var removed = false;

            if (HostDictionary.TryRemove(name, out _))
            {
                removed = true;
            }

            AllRepositories = HostDictionary.Values
                .Select(value => value.Repository)
                .Prepend(Local.Repository);

            State.SetValue(state => state with
            {
                Hosts = Hosts.Select(host => host.Name).ToArray(),
                Directories = Hosts.SelectMany(host => host.Shares).Sum(share => share.Directories ?? 0),
                Files = Hosts.SelectMany(host => host.Shares).Sum(share => share.Files ?? 0),
            });

            return removed;
        }

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        public Task<Directory> ListDirectoryAsync(string directory)
        {
            var files = AllRepositories.SelectMany(r => r.ListFiles(directory));

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
        public Task<(string Host, string Filename, long Size)> ResolveFileAsync(string remoteFilename)
        {
            var (resolvedFilename, size) = Local.Repository.FindFileInfo(remoteFilename);

            // when we're debugging the relay (running as both controller and agent on the same instance)
            // always resolve files from remote hosts, ignoring local. this is a crappy hack, but it is the
            // least crappy way to maintain sanity while trying to debug this feature.
            if (OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Debug)
            {
                Log.Warning("Ignoring resolved local file to facilitate Relay debugging.");
            }
            else if (!string.IsNullOrEmpty(resolvedFilename))
            {
                Log.Debug("Resolved remote file to {ResolvedFilename} on local host", resolvedFilename);
                return Task.FromResult((Program.LocalHostName, resolvedFilename, size));
            }
            else
            {
                Log.Debug("Failed to resolve remote file on local host; searching remote hosts: {Hosts}", HostDictionary.Values.Select(h => h.Host.Name));
            }

            // file not found locally.  begin searching other hosts one by one.
            // this is the slow, dumb way to do this, but it's plenty fast in this context.
            foreach (var host in HostDictionary.Values)
            {
                (resolvedFilename, size) = host.Repository.FindFileInfo(remoteFilename);

                if (!string.IsNullOrEmpty(resolvedFilename))
                {
                    Log.Debug("Resolved remote file to {ResolvedFilename} on host {Host}", remoteFilename, host.Host.Name);
                    return Task.FromResult((host.Host.Name, resolvedFilename, size));
                }

                Log.Debug("Failed to resolve remote file on host {Host}", host.Host.Name);
            }

            throw new NotFoundException($"The requested filename '{remoteFilename}' could not be resolved to a physical file");
        }

        /// <summary>
        ///     Returns the list of all <see cref="Scan"/>  started at or after the specified <paramref name="startedAtOrAfter"/>
        ///     unix timestamp.
        /// </summary>
        /// <param name="startedAtOrAfter">A unix timestamp that serves as the lower bound of the time-based listing.</param>
        /// <returns>The operation context, including the list of found scans.</returns>
        public Task<IEnumerable<Scan>> ListScansAsync(long startedAtOrAfter = 0)
            => Task.FromResult(Local.Repository.ListScans(startedAtOrAfter));

        /// <summary>
        ///     Requests that a share scan is performed.
        /// </summary>
        public void RequestScan()
        {
            Local.Repository.FlagLatestScanAsSuspect();
            State.SetValue(state => state with { ScanPending = true });
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public Task<IEnumerable<File>> SearchAsync(SearchQuery query)
        {
            var results = AllRepositories.SelectMany(r => r.Search(query));

            return Task.FromResult(results);
        }

        /// <summary>
        ///     Scans the configured shares.
        /// </summary>
        /// <returns>The operation context.</returns>
        /// <exception cref="ShareScanInProgressException">Thrown when a scan is already in progress.</exception>
        public async Task ScanAsync()
        {
            // try to obtain the semaphore, or fail if it has already been obtained
            // the scanner has an identical check, but because this method changes state we need it here, too
            if (!await ScannerSyncRoot.WaitAsync(millisecondsTimeout: 0))
            {
                Log.Warning("Shared file scan rejected; scan is already in progress.");
                throw new ShareScanInProgressException("Shared files are already being scanned.");
            }

            try
            {
                State.SetValue(state => state with { Ready = false });

                await Scanner.ScanAsync(Local.Host.Shares, OptionsMonitor.CurrentValue.Shares, Local.Repository);

                Log.Debug("Backing up shared file cache database...");
                Local.Repository.BackupTo(ShareRepositoryFactory.CreateFromHostBackup(Local.Host.Name));
                Log.Debug("Shared file cache database backup complete");

                Log.Debug("Recomputing share statistics...");
                ComputeShareStatistics();
                Log.Debug("Share statistics updated: {Shares}", Local.Host.Shares.ToJson());

                State.SetValue(state => state with
                {
                    Directories = Hosts.SelectMany(host => host.Shares).Sum(share => share.Directories ?? 0),
                    Files = Hosts.SelectMany(host => host.Shares).Sum(share => share.Files ?? 0),
                    Ready = true,
                });
            }
            finally
            {
                ScannerSyncRoot.Release();
            }
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
            Log.Information("Initializing shares");

            // _probably_ redundant, but to be safe
            State.SetValue(state => state with { Ready = false });

            try
            {
                if (forceRescan)
                {
                    Log.Warning("Performing a forced re-scan of shares");
                    await ScanAsync();
                }
                else if (CacheStorageMode == StorageMode.Memory)
                {
                    Log.Information("Share cache StorageMode is 'Memory'. Attempting to load from backup...");

                    var backupRepository = ShareRepositoryFactory.CreateFromHostBackup(Local.Host.Name);

                    if (backupRepository.TryValidate(out _))
                    {
                        Log.Information("Share cache backup validated. Attempting to restore...");

                        Local.Repository.RestoreFrom(backupRepository);

                        Log.Information("Share cache successfully restored from backup");
                    }
                    else
                    {
                        Log.Warning("Share cache backup is missing, corrupt, or is out of date");
                        throw new ShareInitializationException("Share cache backup is missing, corrupt, or is out of date");
                    }
                }
                else if (CacheStorageMode == StorageMode.Disk)
                {
                    Log.Information("Share cache StorageMode is 'Disk'. Attempting to validate...");

                    if (Local.Repository.TryValidate(out _))
                    {
                        // no-op
                    }
                    else
                    {
                        Log.Warning("Share cache is missing, corrupt, or is out of date. Attempting to load from backup...");

                        var backupRepository = ShareRepositoryFactory.CreateFromHostBackup(Local.Host.Name);

                        if (backupRepository.TryValidate(out _))
                        {
                            Log.Information("Share cache backup validated. Attempting to restore...");

                            Local.Repository.RestoreFrom(backupRepository);

                            Log.Information("Share cache successfully restored from backup");
                        }
                        else
                        {
                            Log.Warning("Share cache and backup are both missing, corrupt, or is out of date");
                            throw new ShareInitializationException("Share cache and backup are both missing, corrupt, or is out of date");
                        }
                    }
                }

                var options = OptionsMonitor.CurrentValue.Shares;
                var latestScan = Local.Repository.FindLatestScan();
                Log.Debug("Latest scan: {Scan}, current options {@Options}", latestScan, options);

                if (latestScan == default)
                {
                    throw new ShareInitializationException("Shares not yet scanned");
                }

                if (!latestScan.EndedAt.HasValue)
                {
                    throw new ShareInitializationException("Previous share scan did not complete");
                }

                if (latestScan.Suspect)
                {
                    throw new ShareInitializationException("Previous share scan was marked as suspect");
                }

                if (latestScan.OptionsJson != options.ToJson())
                {
                    throw new ShareInitializationException("Share options changed since previous scan");
                }

                Local.Repository.EnableKeepalive(true);

                Log.Debug("Recomputing share statistics...");
                ComputeShareStatistics();
                Log.Debug("Share statistics updated");

                // one of several things happened above before we got here:
                //   this method was called with forceRescan = true
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
                    Hosts = Hosts.Select(host => host.Name).ToList().AsReadOnly(),
                    Directories = Hosts.SelectMany(host => host.Shares).Sum(share => share.Directories ?? 0),
                    Files = Hosts.SelectMany(host => host.Shares).Sum(share => share.Files ?? 0),
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error initializing shares: {Message}", ex.Message);

                if (!forceRescan)
                {
                    Log.Warning("Re-attempting initialization");
                    await InitializeAsync(forceRescan: true);
                }
                else
                {
                    Log.Error("Failed to initialize shares, and an attempt to force a full scan to repair failed");
                    throw;
                }
            }
        }

        private void ComputeShareStatistics()
        {
            foreach (var share in Local.Host.Shares)
            {
                var prefix = share.RemotePath + (share.RemotePath.EndsWith('\\') ? string.Empty : '\\');

                var dirs = Local.Repository.CountDirectories(prefix);
                var files = Local.Repository.CountFiles(prefix);

                share.UpdateStatistics(dirs, files);
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

                Local = (Local.Host with { Shares = shares }, Local.Repository);

                LastOptionsHash = optionsHash;
            }
            finally
            {
                SyncRoot.Release();
            }
        }
    }
}
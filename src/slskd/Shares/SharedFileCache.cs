// <copyright file="SharedFileCache.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Serilog;
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

    /// <summary>
    ///     Shared file cache.
    /// </summary>
    public class SharedFileCache : ISharedFileCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharedFileCache"/> class.
        /// </summary>
        /// <param name="storageMode"></param>
        /// <param name="workerCount"></param>
        /// <param name="soulseekFileFactory"></param>
        public SharedFileCache(StorageMode storageMode, int workerCount, ISoulseekFileFactory soulseekFileFactory = null)
        {
            StorageMode = storageMode;
            WorkerCount = workerCount;
            SoulseekFileFactory = soulseekFileFactory ?? new SoulseekFileFactory();
            Repository = new ShareRepository(Program.ConnectionStrings.Shares);
        }

        /// <summary>
        ///     Gets the cache state monitor.
        /// </summary>
        public IStateMonitor<SharedFileCacheState> StateMonitor => State;

        private StorageMode StorageMode { get; }
        private int WorkerCount { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCache>();
        private List<Share> Shares { get; set; }
        private ISoulseekFileFactory SoulseekFileFactory { get; }
        private IManagedState<SharedFileCacheState> State { get; } = new ManagedState<SharedFileCacheState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1);
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private IShareRepository Repository { get; }

        /// <summary>
        ///     Scans the configured shares and fills the cache.
        /// </summary>
        /// <param name="shares">The list of shares from which to fill the cache.</param>
        /// <param name="filters">The list of regular expressions used to exclude files or paths from scanning.</param>
        /// <returns>The operation context.</returns>
        public async Task FillAsync(IEnumerable<Share> shares, IEnumerable<Regex> filters)
        {
            // obtain the semaphore, or fail if it can't be obtained immediately, indicating that a scan is running.
            if (!await SyncRoot.WaitAsync(millisecondsTimeout: 0))
            {
                Log.Warning("Shared file scan rejected; scan is already in progress.");
                throw new ShareScanInProgressException("Shared files are already being scanned.");
            }

            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(Program.MasterCancellationTokenSource.Token);
            var cancellationToken = CancellationTokenSource.Token;

            // send control back to the calling context. if we don't do this, the caller will block until
            // directories have been enumerated.
            await Task.Yield();

            try
            {
                State.SetValue(state => state with
                {
                    Filling = true,
                    Filled = false,
                    Cancelled = false,
                    FillProgress = 0,
                    Directories = 0,
                    Files = 0,
                    ExcludedDirectories = 0,
                });

                // it's possible that the database was tampered with between the time it was checked at startup and now
                // validate the tables, and if there's an issue, drop and recreate everything.
                if (!Repository.TryValidateTables(Program.ConnectionStrings.Shares))
                {
                    Log.Warning("Shared file cache invalid; dropping and re-creating prior to scan.");
                    Repository.CreateTables(discardExisting: true);
                    Log.Information("Share file cache ready.");
                }

                Log.Debug("Starting shared file scan");

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var sw = new Stopwatch();
                var swSnapshot = 0L;
                sw.Start();

                Shares = shares.ToList();

                if (!Shares.Any())
                {
                    Log.Warning("Aborting shared file scan; no shares configured.");
                }

                Shares.ForEach(s => Log.Debug(s.ToJson()));

                Shares.Where(s => !s.IsExcluded).ToList()
                    .ForEach(s => Log.Information("Sharing {Local} as {Remote}", s.LocalPath, s.RemotePath));

                Shares.Where(s => s.IsExcluded).ToList()
                    .ForEach(s => Log.Information("Excluding {Local}", s.LocalPath));

                Log.Information("Enumerating shared directories");
                swSnapshot = sw.ElapsedMilliseconds;

                // derive a list of all directories from all shares skip hidden and system directories, as well as anything that
                // can't be accessed due to security restrictions. it's necessary to enumerate these directories up front so we
                // can deduplicate directories and apply exclusions
                var unmaskedDirectories = Shares
                    .SelectMany(share =>
                    {
                        try
                        {
                            return System.IO.Directory.GetDirectories(share.LocalPath, "*", new EnumerationOptions()
                            {
                                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = true,
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("Failed to scan share {Directory}: {Message}", share.LocalPath, ex.Message);
                            return Array.Empty<string>();
                        }
                    })
                    .Concat(Shares.Select(share => share.LocalPath)) // include the shares themselves (GetDirectories returns only subdirectories)
                    .Where(share => System.IO.Directory.Exists(share)) // discard any directories that don't exist.  we already warned about them.
                    .ToHashSet(); // remove duplicates (in case shares overlap)

                var excludedDirectories = unmaskedDirectories
                    .Where(share => Shares.Where(share => share.IsExcluded).Any(exclusion => share.StartsWith(exclusion.LocalPath)));

                unmaskedDirectories = unmaskedDirectories.Except(excludedDirectories).ToHashSet();

                State.SetValue(state => state with { Directories = unmaskedDirectories.Count, ExcludedDirectories = excludedDirectories.Count() });
                Log.Information("Found {Directories} shared directories (and {Excluded} were excluded) in {Elapsed}ms.  Starting file scan.", unmaskedDirectories.Count, excludedDirectories.Count(), sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                var current = 0;
                var cached = 0;
                var filtered = 0;

                // set up a channel to fan out for directory scanning
                var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
                {
                    AllowSynchronousContinuations = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true,
                });

                // create workers to perform the fan out
                var workers = new List<ChannelReader<string>>();

                foreach (var id in Enumerable.Range(0, WorkerCount))
                {
                    workers.Add(new ChannelReader<string>(
                        channel: channel,
                        cancellationToken: cancellationToken,
                        handler: (directory) =>
                        {
                            Log.Debug("Starting scan of {Directory} ({Current}/{Total})", directory, current + 1, unmaskedDirectories.Count);

                            var addedFiles = 0;
                            var filteredFiles = 0;

                            var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                            Repository.InsertDirectory(directory.ReplaceFirst(share.LocalPath, share.RemotePath), timestamp);

                            // recursively find all files in the directory and stick a record in a dictionary, keyed on the sanitized
                            // filename and with a value of a Soulseek.File object
                            try
                            {
                                // enumerate files in this directory only (no subdirectories) exclude hidden and system files and anything
                                // that can't be accessed due to security restrictions
                                var newFiles = System.IO.Directory.GetFiles(directory, "*", new EnumerationOptions()
                                {
                                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                                    IgnoreInaccessible = true,
                                    RecurseSubdirectories = false,
                                });

                                addedFiles = newFiles.Length;

                                // merge the new dictionary with the rest this will overwrite any duplicate keys, but keys are the fully
                                // qualified name the only time this *should* cause problems is if one of the shares is a subdirectory of another.
                                foreach (var originalFilename in newFiles)
                                {
                                    var info = new FileInfo(originalFilename);
                                    var file = SoulseekFileFactory.Create(originalFilename, maskedFilename: originalFilename.ReplaceFirst(share.LocalPath, share.RemotePath));

                                    if (filters.Any(filter => filter.IsMatch(originalFilename)))
                                    {
                                        filteredFiles++;
                                        continue;
                                    }

                                    Repository.InsertFile(maskedFilename: file.Filename, originalFilename, touchedAt: info.LastWriteTimeUtc, file, timestamp);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("Failed to scan files in directory {Directory}: {Exception}", directory, ex);
                                Log.Warning("Failed to scan files in directory {Directory}: {Message}", directory, ex.Message);
                            }

                            current++;
                            filtered += filteredFiles;
                            cached += addedFiles;

                            Log.Debug("Finished scanning {Directory}: {Added} files added and {Filtered} filtered", directory, addedFiles, filteredFiles);
                            State.SetValue(state => state with { FillProgress = current / (double)unmaskedDirectories.Count, Files = cached });
                        }));
                }

                Log.Debug("Starting workers...");
                workers.ForEach(w => w.Start());
                Log.Debug("All workers started");

                try
                {
                    try
                    {
                        Log.Debug("Filling DirectoryChannel...");
                        foreach (var directory in unmaskedDirectories)
                        {
                            await channel.Writer.WriteAsync(directory, cancellationToken);
                        }

                        Log.Debug("DirectoryChannel filled");
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Warning("Shared file scan cancellation requested");
                        Log.Debug("DirectoryChannel fill aborted");
                    }
                    finally
                    {
                        channel.Writer.Complete();
                    }

                    Log.Debug("Waiting for workers to finish working...");
                    await Task.WhenAll(workers.Select(w => w.Completed));
                    Log.Debug("All workers finished");

                    Log.Information("Scan found {Files} files (and {Filtered} were filtered) in {Elapsed}ms", cached, filtered, sw.ElapsedMilliseconds - swSnapshot);
                    swSnapshot = sw.ElapsedMilliseconds;

                    var deletedFiles = Repository.DeleteFiles(timestamp);
                    var deletedDirectories = Repository.DeleteDirectories(timestamp);

                    Log.Information("Removed or renamed {Files} files and {Directories} directories in {Elapsed}ms", deletedFiles, deletedDirectories, sw.ElapsedMilliseconds - swSnapshot);
                }
                catch (OperationCanceledException)
                {
                    // important! don't try to delete stale files in this case; unscanned records will have a stale timestamp
                    Log.Warning("Shared file scan cancelled successfully");

                    State.SetValue(state => state with { Cancelled = true });
                }

                Log.Debug("Backing up shared file cache database...");
                Repository.Backup(Program.ConnectionStrings.SharesBackup);
                Log.Debug("Shared file cache database backup complete");

                var directoryCount = Repository.CountDirectories();
                var fileCount = Repository.CountFiles();

                State.SetValue(state => state with
                {
                    Filling = false,
                    Faulted = false,
                    Filled = true,
                    FillProgress = 1,
                    Directories = directoryCount,
                    Files = fileCount,
                });

                Log.Debug($"Shared file cache created or updated in {sw.ElapsedMilliseconds}ms.  Directories: {directoryCount}, Files: {fileCount}");
            }
            catch (Exception ex)
            {
                State.SetValue(state => state with { Filling = false, Faulted = true, Filled = false, FillProgress = 0 });
                Log.Warning(ex, "Encountered error during scan of shared files: {Message}", ex.Message);
                throw;
            }
            finally
            {
                CancellationTokenSource = null;
                SyncRoot.Release();
            }
        }

        /// <summary>
        ///     Returns the contents of the specified <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The directory for which the contents are to be listed.</param>
        /// <returns>The contents of the directory.</returns>
        public Directory List(string directory)
        {
            var files = Repository.ListFiles(directory);
            return new Directory(directory, files);
        }

        /// <summary>
        ///     Substitutes the mask in the specified <paramref name="filename"/> with the original path, if the mask is tracked
        ///     by the cache.
        /// </summary>
        /// <param name="filename">The fully qualified filename to unmask.</param>
        /// <returns>The unmasked filename.</returns>
        public string Resolve(string filename)
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT originalFilename FROM files WHERE maskedFilename = @maskedFilename;", conn);
            cmd.Parameters.AddWithValue("maskedFilename", filename);

            var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                Log.Warning("Failed to resolve shared file {Filename}", filename);
                return null;
            }

            var resolved = reader.GetString(0);
            Log.Debug($"Resolved requested shared file {filename} to {resolved}");
            return resolved;
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public IEnumerable<File> Search(SearchQuery query)
        {
            string Clean(string str) => str.Replace("/", " ")
                .Replace("\\", " ")
                .Replace(":", " ")
                .Replace("\"", " ")
                .Replace("'", "''");

            var match = string.Join(" AND ", query.Terms.Select(token => $"\"{Clean(token)}\""));
            var exclusions = string.Join(" OR ", query.Exclusions.Select(exclusion => $"\"{Clean(exclusion)}\""));

            var sql = $"SELECT files.maskedFilename, files.code, files.size, files.extension, files.attributeJson FROM filenames " +
                "INNER JOIN files ON filenames.maskedFilename = files.maskedFilename " +
                $"WHERE filenames MATCH '({match}) {(query.Exclusions.Any() ? $"NOT ({exclusions})" : string.Empty)}' " +
                "ORDER BY filenames.maskedFilename ASC;";

            var results = new List<File>();
            SqliteDataReader reader = default;

            try
            {
                using var conn = GetConnection();
                using var cmd = new SqliteCommand(sql, conn);
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var filename = reader.GetString(0);
                    var code = reader.GetInt32(1);
                    var size = reader.GetInt64(2);
                    var extension = reader.GetString(3);
                    var attributeJson = reader.GetString(4);

                    var attributeList = attributeJson.FromJson<List<FileAttribute>>();

                    var file = new File(code, filename, size, extension, attributeList);
                    results.Add(file);
                }

                return results;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to execute shared file query '{Query}': {Message}", query, ex.Message);
                return Enumerable.Empty<File>();
            }
        }

        /// <summary>
        ///     Cancels the currently running fill operation, if one is running.
        /// </summary>
        /// <returns>A value indicating whether a fill operation was cancelled.</returns>
        public bool TryCancelFill()
        {
            if (CancellationTokenSource != null)
            {
                CancellationTokenSource.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Attempts to load the cache from disk.
        /// </summary>
        /// <returns>A value indicating whether the load was successful.</returns>
        public bool TryLoad()
        {
            try
            {
                // see if we need to 'restore' the database from disk, and do so
                if (StorageMode == StorageMode.Memory || (StorageMode == StorageMode.Disk && !Repository.TryValidateTables(Program.ConnectionStrings.Shares)))
                {
                    Log.Debug($"Share cache {(StorageMode == StorageMode.Memory ? "StorageMode is 'Memory'" : "database is missing from disk")}. Attempting to load from backup...");

                    // the backup is missing; we can't do anything but recreate it from scratch
                    if (!Repository.TryValidateTables(Program.ConnectionStrings.SharesBackup))
                    {
                        Log.Debug("Share cache backup is missing; unable to restore");
                        return false;
                    }

                    Log.Debug("Share cache backup located. Attempting to restore...");

                    using var backupConn = GetConnection(Program.ConnectionStrings.SharesBackup);
                    using var conn = GetConnection();
                    backupConn.BackupDatabase(conn);

                    Log.Debug("Share cache successfully restored from backup");
                }

                // one of several thigns happened above before we got here:
                //   the storage mode is memory, and we loaded the in-memory db from a valid backup
                //   the storage mode is disk, and the file is there and valid
                //   the storage mode is disk but either missing or invalid, and we restored from a valid backup
                // at this point there is a valid (existing, schema matching expected schema) database at the primary connection string
                State.SetValue(state => state with
                {
                    Filling = false,
                    Faulted = false,
                    Filled = true,
                    FillProgress = 1,
                    Directories = Repository.CountDirectories(),
                    Files = Repository.CountFiles(),
                });

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error loading shared file cache: {Message}", ex.Message);
                return false;
            }
        }

        private SqliteConnection GetConnection(string connectionString = null)
        {
            var conn = new SqliteConnection(connectionString ?? Program.ConnectionStrings.Shares);
            conn.Open();
            return conn;
        }
    }
}
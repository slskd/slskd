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
    using System.Collections.Concurrent;
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
    public class SharedFileCache : ISharedFileCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharedFileCache"/> class.
        /// </summary>
        /// <param name="storageMode"></param>
        /// <param name="soulseekFileFactory"></param>
        public SharedFileCache(StorageMode storageMode, int workerCount, ISoulseekFileFactory soulseekFileFactory = null)
        {
            StorageMode = storageMode;
            WorkerCount = workerCount;
            SoulseekFileFactory = soulseekFileFactory ?? new SoulseekFileFactory();
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
        private Channel<string> DirectoryChannel { get; set; }

        /// <summary>
        ///     Returns the contents of the cache.
        /// </summary>
        /// <returns>The contents of the cache.</returns>
        public IEnumerable<Directory> Browse()
        {
            if (!State.CurrentValue.Filled)
            {
                if (State.CurrentValue.Filling)
                {
                    return new[] { new Directory("Scanning shares, please check back in a few minutes.") };
                }

                return Enumerable.Empty<Directory>();
            }

            var directories = new ConcurrentDictionary<string, Directory>();

            // Soulseek requires that each directory in the tree have an entry in the list returned in a browse response. if
            // missing, files that are nested within directories which contain only directories (no files) are displayed as being
            // in the root. to get around this, prime a dictionary with all known directories, and an empty Soulseek.Directory. if
            // there are any files in the directory, this entry will be overwritten with a new Soulseek.Directory containing the
            // files. if not they'll be left as is.
            foreach (var directory in ListDirectories())
            {
                directories.TryAdd(directory, new Directory(directory));
            }

            var files = new List<File>();

            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files ORDER BY maskedFilename ASC;", conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var filename = reader.GetString(0);
                var code = reader.GetInt32(1);
                var size = reader.GetInt64(2);
                var extension = reader.GetString(3);
                var attributeJson = reader.GetString(4);

                var attributeList = attributeJson.FromJson<List<FileAttribute>>();

                var file = new File(code, filename, size, extension, attributeList);

                files.Add(file);
            }

            var groups = files
                .GroupBy(file => Path.GetDirectoryName(file.Filename))
                .Select(group => new Directory(group.Key, group.Select(f =>
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

            return directories.Values.OrderBy(d => d.Name);
        }

        /// <summary>
        ///     Scans the configured shares and fills the cache.
        /// </summary>
        /// <param name="shares">The list of shares from which to fill the cache.</param>
        /// <param name="filters">The list of regular expressions used to exclude files or paths from scanning.</param>
        /// <returns>The operation context.</returns>
        public async Task FillAsync(IEnumerable<Share> shares, IEnumerable<Regex> filters, CancellationToken cancellationToken = default)
        {
            // obtain the semaphore, or fail if it can't be obtained immediately, indicating that a scan is running.
            if (!await SyncRoot.WaitAsync(millisecondsTimeout: 0))
            {
                Log.Warning("Shared file scan rejected; scan is already in progress.");
                throw new ShareScanInProgressException("Shared files are already being scanned.");
            }

            try
            {
                // drop and recreate the tables to clear them
                // note: leave the backup in place. this will leave the app in a better position if the app stops during the scan.
                CreateTables();

                State.SetValue(state => state with
                {
                    Filling = true,
                    Filled = false,
                    FillProgress = 0,
                    Directories = 0,
                    Files = 0,
                    ExcludedDirectories = 0,
                });

                Log.Debug("Starting shared file scan");

                await Task.Yield();

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

                Log.Debug("Enumerating shared directories");
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
                Log.Debug("Found {Directories} shared directories (and {Excluded} were excluded) in {Elapsed}ms.  Starting file scan.", unmaskedDirectories.Count, excludedDirectories.Count(), sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                var maskedDirectories = new HashSet<string>();
                var current = 0;
                var cached = 0;
                var filtered = 0;

                // set up a channel to fan out for directory scanning
                DirectoryChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
                {
                    AllowSynchronousContinuations = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true,
                });

                // create workers to perform the fan out
                var workers = new List<SharedFileCacheWorker>();

                foreach (var id in Enumerable.Range(0, WorkerCount))
                {
                    workers.Add(new SharedFileCacheWorker(
                        id: id,
                        directoryChannel: DirectoryChannel,
                        cancellationToken: cancellationToken,
                        handler: (directory) =>
                        {
                            Log.Debug("Starting scan of {Directory} ({Current}/{Total})", directory, current + 1, unmaskedDirectories.Count);

                            var addedFiles = 0;
                            var filteredFiles = 0;

                            var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                            maskedDirectories.Add(directory.ReplaceFirst(share.LocalPath, share.RemotePath));
                            InsertDirectory(directory.ReplaceFirst(share.LocalPath, share.RemotePath));

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

                                addedFiles = newFiles.Count();

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

                                    InsertFile(maskedFilename: file.Filename, originalFilename, touchedAt: info.LastWriteTimeUtc, file);
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

                            try
                            {
                                State.SetValue(state => state with { FillProgress = current / (double)unmaskedDirectories.Count, Files = cached });
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to set cache state following scan of {Directory}: {Message}", directory, ex.Message);
                                throw;
                            }
                        }));
                }

                Log.Debug("Starting workers...");
                workers.ForEach(w => w.Start());
                Log.Debug("All workers started");

                Log.Debug("Filling DirectoryChannel...");
                foreach (var directory in unmaskedDirectories)
                {
                    await DirectoryChannel.Writer.WriteAsync(directory);
                }

                DirectoryChannel.Writer.Complete();
                Log.Debug("DirectoryChannel filled");

                Log.Debug("Waiting for workers to finish working...");
                await Task.WhenAll(workers.Select(w => w.Completed));
                Log.Debug("All workers finished");

                Log.Debug("Directory scan found {Files} files (and {Filtered} were filtered) in {Elapsed}ms.  Populating filename database", cached, filtered, sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                Log.Debug("Inserted {Files} records in {Elapsed}ms", cached, sw.ElapsedMilliseconds - swSnapshot);

                Log.Debug("Backing up shared file cache database...");
                Backup();
                Log.Debug("Shared file cache database backup complete");

                State.SetValue(state => state with
                {
                    Filling = false,
                    Faulted = false,
                    Filled = true,
                    FillProgress = 1,
                    Directories = CountDirectories(),
                    Files = CountFiles(),
                });

                Log.Debug($"Shared file cache recreated in {sw.ElapsedMilliseconds}ms.  Directories: {unmaskedDirectories.Count}, Files: {cached}");
            }
            catch (Exception ex)
            {
                State.SetValue(state => state with { Filling = false, Faulted = true, Filled = false, FillProgress = 0 });
                Log.Warning(ex, "Encountered error during scan of shared files: {Message}", ex.Message);
                throw;
            }
            finally
            {
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
            if (!State.CurrentValue.Filled)
            {
                if (State.CurrentValue.Filling)
                {
                    return new Directory(directory, new[] { new File(1, "Scanning shares, please check back in a few minutes.", 0, string.Empty) });
                }

                return null;
            }

            var files = ListFiles(directory);
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
            if (!State.CurrentValue.Filled)
            {
                return Enumerable.Empty<File>();
            }

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
        ///     Attempts to load the cache from disk.
        /// </summary>
        /// <returns>A value indicating whether the load was successful.</returns>
        public bool TryLoad()
        {
            try
            {
                // see if we need to 'restore' the database from disk, and do so
                if (StorageMode == StorageMode.Memory || (StorageMode == StorageMode.Disk && !ValidateTables(Program.ConnectionStrings.Shares)))
                {
                    Log.Debug($"Share cache {(StorageMode == StorageMode.Memory ? "StorageMode is 'Memory'" : "database is missing from disk")}. Attempting to load from backup...");

                    // the backup is missing; we can't do anything but recreate it from scratch
                    if (!ValidateTables(Program.ConnectionStrings.SharesBackup))
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
                    Directories = CountDirectories(),
                    Files = CountFiles(),
                });

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error loading shared file cache: {Message}", ex.Message);
                return false;
            }
        }

        private bool ValidateTables(string connectionString)
        {
            var schema = new Dictionary<string, string>()
            {
                { "directories", "CREATE TABLE directories (name TEXT PRIMARY KEY)" },
                { "filenames", "CREATE VIRTUAL TABLE filenames USING fts5(maskedFilename)" },
                { "files", "CREATE TABLE files (maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, extension TEXT, attributeJson TEXT NOT NULL)" },
            };

            try
            {
                Log.Debug("Validating shares database with connection string {String}", connectionString);

                using var conn = GetConnection(connectionString);
                using var cmd = new SqliteCommand("SELECT name, sql from sqlite_master WHERE type = 'table' AND name IN ('directories', 'filenames', 'files');", conn);

                var reader = cmd.ExecuteReader();
                var rows = 0;

                while (reader.Read())
                {
                    var table = reader.GetString(0);
                    var expectedSql = reader.GetString(1);

                    if (schema.TryGetValue(table, out var actualSql))
                    {
                        if (actualSql != expectedSql)
                        {
                            throw new MissingFieldException($"Expected {table} schema to be {expectedSql}, found {actualSql}");
                        }
                        else
                        {
                            Log.Debug("Shares database table {Table} is valid: {Actual}", table, actualSql);
                        }
                    }

                    rows++;
                }

                if (rows != schema.Count)
                {
                    throw new MissingMemberException($"Expected {schema.Count} tables, found {rows}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, $"Failed to validate shares database with connection string {connectionString}: {ex.Message}");
                return false;
            }
        }

        private void Backup()
        {
            using var sourceConn = GetConnection();
            using var backupConn = GetConnection(Program.ConnectionStrings.SharesBackup);
            sourceConn.BackupDatabase(backupConn);
        }

        private int CountDirectories()
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM directories;", conn);
            var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        private int CountFiles()
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM files;", conn);
            var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        private void CreateTables()
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("DROP TABLE IF EXISTS directories; CREATE TABLE directories (name TEXT PRIMARY KEY);");

            conn.ExecuteNonQuery("DROP TABLE IF EXISTS filenames; CREATE VIRTUAL TABLE filenames USING fts5(maskedFilename);");

            conn.ExecuteNonQuery("DROP TABLE IF EXISTS files; CREATE TABLE files " +
                "(maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, " +
                "extension TEXT, attributeJson TEXT NOT NULL);");
        }

        private SqliteConnection GetConnection(string connectionString = null)
        {
            var conn = new SqliteConnection(connectionString ?? Program.ConnectionStrings.Shares);
            conn.Open();
            return conn;
        }

        private void InsertDirectory(string name)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO directories VALUES(@name) ON CONFLICT DO NOTHING;", cmd =>
            {
                cmd.Parameters.AddWithValue("name", name);
            });
        }

        private void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, File file)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO files (maskedFilename, originalFilename, size, touchedAt, code, extension, attributeJson) " +
                "VALUES(@maskedFilename, @originalFilename, @size, @touchedAt, @code, @extension, @attributeJson) " +
                "ON CONFLICT DO UPDATE SET originalFilename = excluded.originalFilename, size = excluded.size, touchedAt = excluded.touchedAt, code = excluded.code, extension = excluded.extension, attributeJson = excluded.attributeJson;", cmd =>
                {
                    cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);
                    cmd.Parameters.AddWithValue("originalFilename", originalFilename);
                    cmd.Parameters.AddWithValue("size", file.Size);
                    cmd.Parameters.AddWithValue("touchedAt", touchedAt.ToLongDateString());
                    cmd.Parameters.AddWithValue("code", file.Code);
                    cmd.Parameters.AddWithValue("extension", file.Extension);
                    cmd.Parameters.AddWithValue("attributeJson", file.Attributes.ToJson());
                });

            conn.ExecuteNonQuery("INSERT INTO filenames (maskedFilename) VALUES(@maskedFilename);", cmd =>
            {
                cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);
            });
        }

        private IEnumerable<string> ListDirectories()
        {
            var results = new List<string>();

            using var conn = GetConnection();

            using var cmd = new SqliteCommand("SELECT name FROM directories ORDER BY name ASC;", conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }

        private IEnumerable<File> ListFiles(string directory = null)
        {
            var results = new List<File>();

            SqliteCommand cmd = default;
            using var conn = GetConnection();

            try
            {
                if (string.IsNullOrEmpty(directory))
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files ORDER BY maskedFilename ASC;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files WHERE maskedFilename LIKE @match ORDER BY maskedFilename ASC;", conn);
                    cmd.Parameters.AddWithValue("match", directory + '%');
                }

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var filename = reader.GetString(0);
                    var code = reader.GetInt32(1);
                    var size = reader.GetInt64(2);
                    var extension = reader.GetString(3);
                    var attributeJson = reader.GetString(4);

                    var attributeList = attributeJson.FromJson<List<FileAttribute>>();

                    var file = new File(code, filename: Path.GetFileName(filename), size, extension, attributeList);

                    results.Add(file);
                }

                return results;
            }
            finally
            {
                cmd?.Dispose();
            }
        }
    }
}
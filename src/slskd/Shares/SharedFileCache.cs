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
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
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
        ///     Returns the contents of the cache.
        /// </summary>
        /// <returns>The contents of the cache.</returns>
        IEnumerable<Directory> Browse();

        /// <summary>
        ///     Scans the configured shares and fills the cache.
        /// </summary>
        /// <remarks>Initiates the scan, then yields execution back to the caller; does not wait for the operation to complete.</remarks>
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
        public string Resolve(string filename);

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        Task<IEnumerable<File>> SearchAsync(SearchQuery query);
    }

    /// <summary>
    ///     Shared file cache.
    /// </summary>
    public class SharedFileCache : ISharedFileCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharedFileCache"/> class.
        /// </summary>
        /// <param name="soulseekFileFactory"></param>
        public SharedFileCache(ISoulseekFileFactory soulseekFileFactory = null)
        {
            SoulseekFileFactory = soulseekFileFactory ?? new SoulseekFileFactory();
        }

        /// <summary>
        ///     Gets the cache state monitor.
        /// </summary>
        public IStateMonitor<SharedFileCacheState> StateMonitor => State;

        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCache>();
        private Dictionary<string, File> MaskedFiles { get; set; }
        private List<Share> Shares { get; set; }
        private ISoulseekFileFactory SoulseekFileFactory { get; }
        private SqliteConnection SQLite { get; set; }
        private IManagedState<SharedFileCacheState> State { get; } = new ManagedState<SharedFileCacheState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1);

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
                    yield return new Directory("Scanning shares, please check back in a few minutes.");
                }

                yield break;
            }

            foreach (var directory in ListDirectories().OrderBy(d => d))
            {
                var files = ListFiles(directory);
                yield return new Directory(directory, files);
            }
        }

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

            try
            {
                State.SetValue(state => state with
                {
                    Filling = true,
                    Filled = false,
                    FillProgress = 0,
                });

                Log.Debug("Starting shared file scan");

                await Task.Yield();

                ResetCache();

                var scannedAt = DateTime.UtcNow;
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

                // derive a list of all directories from all shares
                // skip hidden and system directories, as well as anything that can't be accessed due to security restrictions.
                // it's necessary to enumerate these directories up front so we can deduplicate directories and apply exclusions
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

                var files = new Dictionary<string, File>();
                var maskedDirectories = new HashSet<string>();
                var current = 0L;
                var filtered = 0L;

                foreach (var directory in unmaskedDirectories)
                {
                    Log.Debug("Starting scan of {Directory} ({Current}/{Total})", directory, current + 1, unmaskedDirectories.Count);

                    if (Path.DirectorySeparatorChar == '/' && directory.Contains('\\'))
                    {
                        Log.Warning("Directory name {Directory} contains one or more backslashes, which are not currently supported. This directory will not be shared.", directory);
                        continue;
                    }

                    var addedFiles = 0L;
                    var filteredFiles = 0L;

                    var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                    maskedDirectories.Add(directory.ReplaceFirst(share.LocalPath, share.RemotePath));
                    InsertDirectory(directory.ReplaceFirst(share.LocalPath, share.RemotePath));

                    // recursively find all files in the directory and stick a record in a dictionary, keyed on the sanitized
                    // filename and with a value of a Soulseek.File object
                    try
                    {
                        // enumerate files in this directory only (no subdirectories)
                        // exclude hidden and system files and anything that can't be accessed due to security restrictions
                        var newFiles = System.IO.Directory.GetFiles(directory, "*", new EnumerationOptions()
                        {
                            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = false,
                        })
                            .Select(filename => SoulseekFileFactory.Create(filename, maskedFilename: filename.ReplaceFirst(share.LocalPath, share.RemotePath)))
                            .ToDictionary(file => file.Filename, file => file);

                        addedFiles = newFiles.Count;

                        // merge the new dictionary with the rest this will overwrite any duplicate keys, but keys are the fully
                        // qualified name the only time this *should* cause problems is if one of the shares is a subdirectory of another.
                        foreach (var file in newFiles)
                        {
                            if (filters.Any(filter => filter.IsMatch(file.Key)))
                            {
                                filteredFiles++;
                                continue;
                            }

                            if (Path.DirectorySeparatorChar == '/' && file.Value.Filename.Contains('\\'))
                            {
                                Log.Warning("Filename {Filename} contains one or more backslashes, which are not currently supported. This file will not be shared.", file.Value.Filename);
                                continue;
                            }

                            if (files.ContainsKey(file.Key))
                            {
                                Log.Warning($"File {file.Key} shared in directory {directory} has already been cached.  This is probably a misconfiguration of the shared directories (is a subdirectory being re-shared?).");
                            }

                            files[file.Key] = file.Value;
                            InsertFile(maskedFilename: file.Key, originalFilename: file.Key, scannedAt, file.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Failed to scan files in directory {Directory}: {Exception}", directory, ex);
                        Log.Warning("Failed to scan files in directory {Directory}: {Message}", directory, ex.Message);
                    }

                    current++;
                    filtered += filteredFiles;

                    Log.Debug("Finished scanning {Directory}: {Added} files added and {Filtered} filtered", directory, addedFiles, filteredFiles);

                    try
                    {
                        State.SetValue(state => state with { FillProgress = current / (double)unmaskedDirectories.Count, Files = files.Count });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to set cache state following scan of {Directory}: {Message}", directory, ex.Message);
                        throw;
                    }
                }

                Log.Debug("Directory scan found {Files} files (and {Filtered} were filtered) in {Elapsed}ms.  Populating filename database", files.Count, filtered, sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                //// potentially optimize with multi-valued insert https://stackoverflow.com/questions/16055566/insert-multiple-rows-in-sqlite
                //foreach (var file in files)
                //{
                //    InsertFile(maskedFilename: file.Key, file.Value);
                //}

                Log.Debug("Inserted {Files} records in {Elapsed}ms", files.Count, sw.ElapsedMilliseconds - swSnapshot);

                MaskedFiles = files;

                State.SetValue(state => state with
                {
                    Filling = false,
                    Faulted = false,
                    Filled = true,
                    FillProgress = 1,
                    Directories = CountDirectories(),
                    Files = CountFiles(),
                });

                Log.Debug($"Shared file cache recreated in {sw.ElapsedMilliseconds}ms.  Directories: {unmaskedDirectories.Count}, Files: {files.Count}");
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
            // ensure this is a tracked file
            if (!MaskedFiles.TryGetValue(filename, out _))
            {
                return null;
            }

            var resolved = filename;

            // a well-formed path will consist of a mask, either an alias or a local directory, and a fully qualified path to a
            // file. split the requested filename so we can examine the first two segments
            var parts = filename.Split(new[] { '/', '\\' });

            if (parts.Length < 2)
            {
                Log.Warning($"Failed to resolve shared file {filename}; filename is badly formed");
                return null;
            }

            // find the share with a matching mask and alias/local directory
            var share = Shares.FirstOrDefault(share => share.Alias == parts[0]);

            if (share == default)
            {
                Log.Warning("Failed to resolve shared file {Filename}; unable to resolve share from alias '{Alias}'", filename, parts[0]);
                return null;
            }

            resolved = resolved.ReplaceFirst(share.RemotePath, share.LocalPath);

            Log.Debug($"Resolved requested shared file {filename} to {resolved}");
            return resolved;
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public async Task<IEnumerable<File>> SearchAsync(SearchQuery query)
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

            var sql = $"SELECT * FROM filenames WHERE filenames MATCH '({match}) {(query.Exclusions.Any() ? $"NOT ({exclusions})" : string.Empty)}'";
            var results = new List<string>();

            try
            {
                using var conn = new SqliteConnection(Program.ConnectionStrings.Shares);
                using var cmd = new SqliteCommand(sql, conn);
                await conn.OpenAsync();

                var reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    results.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to execute shared file query '{Query}': {Message}", query, ex.Message);
                return Enumerable.Empty<File>();
            }

            try
            {
                return results
                    .Select(r => MaskedFiles[r])
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to map shared file result: {Message}", ex.Message);
                return Enumerable.Empty<File>();
            }
        }

        private void CreateTables()
        {
            if (SQLite != null)
            {
                SQLite.Dispose();
            }

            SQLite = new SqliteConnection(Program.ConnectionStrings.Shares);
            SQLite.Open();

            SQLite.ExecuteNonQuery("DROP TABLE IF EXISTS directories; CREATE TABLE directories (name TEXT PRIMARY KEY);");

            SQLite.ExecuteNonQuery("DROP TABLE IF EXISTS filenames; CREATE VIRTUAL TABLE filenames USING fts5(maskedFilename);");

            SQLite.ExecuteNonQuery("DROP TABLE IF EXISTS files; CREATE TABLE files " +
                "(maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, " +
                "extension TEXT, attributeJson TEXT NOT NULL);");

            SQLite.ExecuteNonQuery("DROP TABLE IF EXISTS excluded; CREATE TABLE excluded (originalFilename TEXT PRIMARY KEY);");
        }

        private void InsertDirectory(string name)
        {
            SQLite.ExecuteNonQuery("INSERT INTO directories VALUES(@name) ON CONFLICT DO NOTHING;", cmd =>
            {
                cmd.Parameters.AddWithValue("name", name);
            });
        }

        private IEnumerable<string> ListDirectories()
        {
            using var cmd = new SqliteCommand("SELECT * FROM directories;", SQLite);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return reader.GetString(0);
            }
        }

        private IEnumerable<File> ListFiles(string directory = null)
        {
            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(directory))
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files;", SQLite);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files WHERE maskedFilename LIKE @match", SQLite);
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

                    yield return file;
                }
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        private int CountDirectories()
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM directories;", SQLite);
            var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        private int CountFiles()
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM files;", SQLite);
            var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        private void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, File file)
        {
            SQLite.ExecuteNonQuery("INSERT INTO filenames(maskedFilename) VALUES(@maskedFilename);", cmd =>
            {
                cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);
            });

            SQLite.ExecuteNonQuery("INSERT INTO files (maskedFilename, originalFilename, size, touchedAt, code, extension, attributeJson) " +
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
        }

        private void ResetCache()
        {
            CreateTables();
            MaskedFiles = new Dictionary<string, File>();
            State.SetValue(state => state with { Directories = 0, Files = 0 });
        }
    }
}
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
        private HashSet<string> MaskedDirectories { get; set; }
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
            foreach (var directory in MaskedDirectories)
            {
                directories.TryAdd(directory, new Directory(directory));
            }

            var groups = MaskedFiles
                .GroupBy(f => Path.GetDirectoryName(f.Key))
                .Select(g => new Directory(g.Key, g.Select(g =>
                {
                    var f = g.Value;
                    return new File(
                        f.Code,
                        Path.GetFileName(f.Filename), // we can send the full path, or just the filename.  save bandwidth and omit the path.
                        f.Size,
                        f.Extension,
                        f.Attributes);
                })));

            // merge the dictionary containing all directories with the Soulseek.Directory instances containing their files.
            // entries with no files will remain untouched.
            foreach (var group in groups)
            {
                directories.AddOrUpdate(group.Name, group, (_, _) => group);
            }

            return directories.Values.OrderBy(f => f.Name);
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

                    var addedFiles = 0L;
                    var filteredFiles = 0L;

                    var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                    maskedDirectories.Add(directory.ReplaceFirst(share.LocalPath, share.RemotePath));

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
                        foreach (var record in newFiles)
                        {
                            var (key, value) = record;

                            if (filters.Any(filter => filter.IsMatch(key)))
                            {
                                filteredFiles++;
                                continue;
                            }

                            if (files.ContainsKey(key))
                            {
                                Log.Warning($"File {key} shared in directory {directory} has already been cached.  This is probably a misconfiguration of the shared directories (is a subdirectory being re-shared?).");
                            }

                            // if we're on an operating system that uses forward slashes, it is possible for filenames to contain
                            // backslashes. because we normalize directory separators to backslashes before we send results to the network,
                            // we need to replace any backslashes with a placeholder to prevent things from breaking. we'll capture the original
                            // filename if we do this so we can resolve the original filename given the replacement without doing any guessing.
                            if (Path.DirectorySeparatorChar == '/' && value.Filename.Contains('\\'))
                            {
                                // todo: store OriginalFilename for all files once masked filenames are stored in the db. we aren't doing this now to reduce memory footprint.
                                value = new File(
                                    value.Code,
                                    value.Filename.Replace('\\', '_'),
                                    value.Size,
                                    value.Extension,
                                    value.Attributes,
                                    originalFilename: value.Filename);

                                Log.Warning($"Substituting {value.Filename} for {value.Filename.Replace('\\', '_')}");
                            }

                            files[key] = value;
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

                // potentially optimize with multi-valued insert https://stackoverflow.com/questions/16055566/insert-multiple-rows-in-sqlite
                foreach (var file in files)
                {
                    InsertFilename(file.Key);
                }

                Log.Debug("Inserted {Files} records in {Elapsed}ms", files.Count, sw.ElapsedMilliseconds - swSnapshot);

                MaskedDirectories = maskedDirectories;
                MaskedFiles = files;

                State.SetValue(state => state with
                {
                    Filling = false,
                    Faulted = false,
                    Filled = true,
                    FillProgress = 1,
                    Directories = MaskedDirectories.Count,
                    Files = MaskedFiles.Count,
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

            var files = MaskedFiles.Where(f => f.Key.StartsWith(directory)).Select(kvp =>
            {
                var f = kvp.Value;
                return new File(
                    f.Code,
                    Path.GetFileName(f.Filename),
                    f.Size,
                    f.Extension,
                    f.Attributes);
            });

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
            if (!MaskedFiles.TryGetValue(filename, out var maskedRecord))
            {
                return null;
            }

            // if the OriginalFilename property of the record was set during the scan,
            // one or more characters was substituted, and we can't trust the manipulated
            // path to find the correct file without some degree of guessing. return the
            // value of OriginalFilename which definitively points to the location on disk.
            if (!string.IsNullOrEmpty(maskedRecord.Filename))
            {
                // todo: use OriginalFilename for all files once masked files are stored in the db
                return maskedRecord.OriginalFilename;
            }

            // if OriginalFilename isn't set, derive the location of the file from the masked
            // filename and share configuration. we don't store OriginalFilename for all records
            // to reduce memory footprint
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

            var sql = $"SELECT * FROM cache WHERE cache MATCH '({match}) {(query.Exclusions.Any() ? $"NOT ({exclusions})" : string.Empty)}'";
            var results = new List<string>();

            try
            {
                using var conn = new SqliteConnection($"Data Source={Path.Combine(Program.AppDirectory, "data", "shares.db")};cache=shared");
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

        private void CreateTable()
        {
            if (SQLite != null)
            {
                SQLite.Dispose();
            }

            SQLite = new SqliteConnection($"Data Source={Path.Combine(Program.AppDirectory, "data", "shares.db")};cache=shared");
            SQLite.Open();

            using var cmd = new SqliteCommand("DROP TABLE IF EXISTS cache; CREATE VIRTUAL TABLE cache USING fts5(filename);", SQLite);
            cmd.ExecuteNonQuery();
        }

        private void InsertFilename(string filename)
        {
            using var cmd = new SqliteCommand($"INSERT INTO cache(filename) VALUES(@filename)", SQLite);
            cmd.Parameters.AddWithValue("filename", filename);
            cmd.ExecuteNonQuery();
        }

        private void ResetCache()
        {
            CreateTable();
            MaskedDirectories = new HashSet<string>();
            MaskedFiles = new Dictionary<string, File>();
            State.SetValue(state => state with { Directories = 0, Files = 0 });
        }
    }
}
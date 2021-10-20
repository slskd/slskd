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
using Microsoft.Extensions.Options;

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
        /// <returns>The operation context.</returns>
        Task FillAsync();

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
        /// <param name="optionsMonitor"></param>
        public SharedFileCache(
            IOptionsMonitor<Options> optionsMonitor)
        {
            OptionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Gets the cache state monitor.
        /// </summary>
        public IStateMonitor<SharedFileCacheState> StateMonitor => State;

        private IManagedState<SharedFileCacheState> State { get; } = new ManagedState<SharedFileCacheState>();
        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCache>();
        private HashSet<string> MaskedDirectories { get; set; }
        private Dictionary<string, File> MaskedFiles { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private List<Share> Shares { get; set; }
        private SqliteConnection SQLite { get; set; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1);

        /// <summary>
        ///     Returns the contents of the cache.
        /// </summary>
        /// <returns>The contents of the cache.</returns>
        public IEnumerable<Directory> Browse()
        {
            // ignore requests while the cache is building
            if (SyncRoot.CurrentCount == 0)
            {
                return new[] { new Directory("Scanning shares, please check back in a few minutes.") };
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
        /// <returns>The operation context.</returns>
        public async Task FillAsync()
        {
            // obtain the semaphore, or fail if it can't be obtained immediately, indicating that a scan is running.
            if (!await SyncRoot.WaitAsync(millisecondsTimeout: 0))
            {
                Log.Warning("Shared file scan rejected; scan is already in progress.");
                throw new ShareScanInProgressException("Shared files are already being scanned.");
            }

            try
            {
                await Task.Yield();

                ResetCache();

                State.SetValue(state => state with { Filling = true, FillProgress = 0 });
                Log.Debug("Starting shared file scan");

                var sw = new Stopwatch();
                var swSnapshot = 0L;
                sw.Start();

                Shares = OptionsMonitor.CurrentValue.Directories.Shared
                    .Select(share => share.TrimEnd('/', '\\'))
                    .ToHashSet() // remove duplicates
                    .Select(share => new Share(share)) // convert to Shares
                    .OrderByDescending(share => share.LocalPath.Length) // process subdirectories first.  this allows them to be aliased separately from their parent
                    .ToList();

                if (!Shares.Any())
                {
                    Log.Warning("Aborting shared file scan; no shares configured.");
                }

                foreach (var share in Shares)
                {
                    Log.Debug($"Share: Alias: {share.Alias} Mask: {share.Mask} Local Path: {share.LocalPath} Remote Path: {share.RemotePath} Raw: {share.Raw}");
                }

                Log.Debug("Enumerating shared directories");
                swSnapshot = sw.ElapsedMilliseconds;

                var unmaskedDirectories = Shares
                    .SelectMany(share =>
                    {
                        try
                        {
                            return System.IO.Directory.GetDirectories(share.LocalPath, "*", SearchOption.AllDirectories);
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

                var filters = OptionsMonitor.CurrentValue.Filters.Share
                    .Select(filter => new Regex(filter, RegexOptions.Compiled));

                var files = new Dictionary<string, File>();
                var maskedDirectories = new HashSet<string>();
                var current = 0;
                var filtered = 0;

                foreach (var directory in unmaskedDirectories)
                {
                    var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                    maskedDirectories.Add(directory.ReplaceFirst(share.LocalPath, share.RemotePath));

                    // recursively find all files in the directory and stick a record in a dictionary, keyed on the sanitized
                    // filename and with a value of a Soulseek.File object
                    try
                    {
                        var newFiles = System.IO.Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                            .Select(f => new File(1, f.ReplaceFirst(share.LocalPath, share.RemotePath), new FileInfo(f).Length, Path.GetExtension(f)))
                            .ToDictionary(f => f.Filename, f => f);

                        // merge the new dictionary with the rest this will overwrite any duplicate keys, but keys are the fully
                        // qualified name the only time this *should* cause problems is if one of the shares is a subdirectory of another.
                        foreach (var file in newFiles)
                        {
                            if (filters.Any(filter => filter.IsMatch(file.Key)))
                            {
                                filtered++;
                                continue;
                            }

                            if (files.ContainsKey(file.Key))
                            {
                                Log.Warning($"File {file.Key} shared in directory {directory} has already been cached.  This is probably a misconfiguration of the shared directories (is a subdirectory being re-shared?).");
                            }

                            files[file.Key] = file.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Failed to scan files in directory {Directory}: {Message}", directory, ex.Message);
                    }

                    current++;
                    State.SetValue(state => state with { FillProgress = current / (double)unmaskedDirectories.Count, Files = files.Count });
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

                State.SetValue(state => state with { Filling = false, Faulted = false, FillProgress = 1, Directories = MaskedDirectories.Count, Files = MaskedFiles.Count });
                Log.Debug($"Shared file cache recreated in {sw.ElapsedMilliseconds}ms.  Directories: {unmaskedDirectories.Count}, Files: {files.Count}");
            }
            catch (Exception ex)
            {
                State.SetValue(state => state with { Filling = false, Faulted = true, FillProgress = 0 });
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
            filename = filename.ToLocalOSPath();
            var resolved = filename;

            // a well-formed path will consist of a mask, either an alias or a local directory, and a fully qualified path to a
            // file. split the requested filename so we can examine the first two segments
            var parts = filename.Split(new[] { '/', '\\' });

            if (parts.Length < 2)
            {
                Log.Warning($"Failed to resolve shared file {filename}; filename is badly formed");
                return resolved;
            }

            // find the share with a matching mask and alias/local directory
            var share = Shares.FirstOrDefault(share => share.Mask == parts[0] && share.Alias == parts[1]);

            if (share == default)
            {
                Log.Warning($"Failed to resolve shared file {filename}; unable to resolve share from mask and alias");
                return resolved;
            }

            resolved = resolved.ReplaceFirst(share.RemotePath, share.LocalPath);

            if (resolved == filename) {
                Log.Warning($"Failed to resolve shared file {filename}");
            }

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
            // ignore requests while the cache is building
            if (SyncRoot.CurrentCount == 0)
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
                using var conn = new SqliteConnection("Data Source=file:shares?mode=memory&cache=shared");
                using var cmd = new SqliteCommand(sql, conn);
                await conn.OpenAsync();

                var reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    results.Add(reader.GetString(0));
                }

                return results.Select(r => MaskedFiles[r.Replace("''", "'")]);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to execute shared file query '{Query}': {Message}", query, ex.Message);
                return Enumerable.Empty<File>();
            }
        }

        private void CreateTable()
        {
            if (SQLite != null)
            {
                SQLite.Dispose();
            }

            SQLite = new SqliteConnection("Data Source=file:shares?mode=memory&cache=shared");
            SQLite.Open();

            using var cmd = new SqliteCommand("CREATE VIRTUAL TABLE cache USING fts5(filename)", SQLite);
            cmd.ExecuteNonQuery();
        }

        private void InsertFilename(string filename)
        {
            using var cmd = new SqliteCommand($"INSERT INTO cache(filename) VALUES('{filename.Replace("'", "''")}')", SQLite);
            cmd.ExecuteNonQuery();
        }

        private void ResetCache()
        {
            CreateTable();
            MaskedDirectories = new HashSet<string>();
            MaskedFiles = new Dictionary<string, File>();
            State.SetValue(state => state with { Directories = 0, Files = 0 });
        }

        private class Share
        {
            public Share(string share)
            {
                Raw = share;
                IsExcluded = share.StartsWith('-') || share.StartsWith('!');

                if (IsExcluded)
                {
                    share = share[1..];
                }

                // test to see whether an alias has been specified
                var matches = Regex.Matches(share, @"^\[(.*)\](.*)$");

                if (matches.Any())
                {
                    // split the alias from the path, and validate the alias
                    var groups = matches[0].Groups;
                    Alias = groups[1].Value;
                    LocalPath = groups[2].Value;
                }
                else
                {
                    Alias = share.Split(new[] { '/', '\\' }).Last();
                    LocalPath = share;
                }

                var parent = System.IO.Directory.GetParent(LocalPath).FullName.TrimEnd('/', '\\');

                Mask = Compute.MaskHash(parent);

                var maskedPath = LocalPath.ReplaceFirst(parent, Mask);

                var aliasedSegment = LocalPath[(parent.Length + 1)..];
                RemotePath = maskedPath.ReplaceFirst(aliasedSegment, Alias);
            }

            public string Alias { get; init; }
            public bool IsExcluded { get; init; }
            public string LocalPath { get; init; }
            public string Mask { get; init; }
            public string Raw { get; init; }
            public string RemotePath { get; init; }
        }
    }
}
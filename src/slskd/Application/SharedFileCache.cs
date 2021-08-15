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

namespace slskd
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
        ///     Gets the cache state.
        /// </summary>
        public IStateMonitor<SharedFileCacheState> State { get; } = new StateMonitor<SharedFileCacheState>();

        private Dictionary<string, File> Files { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCache>();
        private HashSet<string> MaskedDirectories { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private List<Share> Shares { get; set; }
        private SqliteConnection SQLite { get; set; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1);
        private HashSet<string> UnmaskedDirectories { get; set; }

        /// <summary>
        ///     Returns the contents of the cache.
        /// </summary>
        /// <returns>The contents of the cache.</returns>
        public IEnumerable<Directory> Browse()
        {
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

            var groups = Files
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

            return directories.Values.OrderBy(f => f.Name[8..]);
        }

        /// <summary>
        ///     Scans the configured shares and fills the cache.
        /// </summary>
        /// <returns>The operation context.</returns>
        public async Task FillAsync()
        {
            if (SyncRoot.CurrentCount == 0)
            {
                Log.Warning("Shared file scan rejected; scan is already in progress.");
                throw new ShareScanInProgressException("Shared files are already being scanned.");
            }

            await Task.Yield();

            await SyncRoot.WaitAsync();

            try
            {
                // todo: don't do this, but rather build a new cache and then swap it in
                ResetCache();

                State.SetValue(state => state with { Filling = true, FillProgress = 0 });
                Log.Debug("Starting shared file scan");

                var sw = new Stopwatch();
                var swSnapshot = 0L;
                sw.Start();

                Shares = OptionsMonitor.CurrentValue.Directories.Shared
                    .ToHashSet() // remove duplicates
                    .Select(share => new Share(share)) // convert to Shares
                    .OrderByDescending(share => share.LocalPath.Length) // process subdirectories first.  this allows them to be aliased separately from their parent
                    .ToList();

                // check for and warn about duplicates
                var duplicates = Shares.GroupBy(share => share.Mask + share.Alias).Where(group => group.Count() > 1);

                if (duplicates.Any())
                {
                    foreach (var dupe in duplicates)
                    {
                        Log.Warning($"Overlapping shares: {string.Join(", ", dupe.Select(s => s.Raw))}. Use different alias(es) to disambiguate; downloads from one of these shares will fail.");
                    }
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
                Log.Debug("Found {Directories} shared directories (and {Excluded} were filtered) in {Elapsed}ms.  Starting file scan.", unmaskedDirectories.Count, excludedDirectories.Count(), sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                var files = new Dictionary<string, File>();
                var maskedDirectories = new HashSet<string>();
                var current = 0;

                foreach (var directory in unmaskedDirectories)
                {
                    var share = Shares.First(share => directory.StartsWith(share.LocalPath));

                    maskedDirectories.Add(directory.ReplaceFirst(share.LocalPath, share.RemotePath));

                    // recursively find all files in the directory and stick a record in a dictionary, keyed on the sanitized
                    // filename and with a value of a Soulseek.File object
                    try
                    {
                        var newFiles = System.IO.Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                            .Select(f => new File(1, f.Replace("/", @"\").ReplaceFirst(share.LocalPath, share.RemotePath), new FileInfo(f).Length, Path.GetExtension(f)))
                            .ToDictionary(f => f.Filename, f => f);

                        // merge the new dictionary with the rest this will overwrite any duplicate keys, but keys are the fully
                        // qualified name the only time this *should* cause problems is if one of the shares is a subdirectory of another.
                        foreach (var file in newFiles)
                        {
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

                Log.Debug("Directory scan found {Files} in {Elapsed}ms.  Populating filename database", files.Count, sw.ElapsedMilliseconds - swSnapshot);
                swSnapshot = sw.ElapsedMilliseconds;

                // potentially optimize with multi-valued insert https://stackoverflow.com/questions/16055566/insert-multiple-rows-in-sqlite
                foreach (var file in files)
                {
                    InsertFilename(file.Key);
                }

                Log.Debug("Inserted {Files} records in {Elapsed}ms", files.Count, sw.ElapsedMilliseconds - swSnapshot);

                UnmaskedDirectories = unmaskedDirectories;
                MaskedDirectories = maskedDirectories;
                Files = files;

                State.SetValue(state => state with { Filling = false, Faulted = false, FillProgress = 1, Directories = UnmaskedDirectories.Count, Files = Files.Count });
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
        ///     Substitutes the mask in the specified <paramref name="filename"/> with the original path, if the mask is tracked
        ///     by the cache.
        /// </summary>
        /// <param name="filename">The fully qualified filename to unmask.</param>
        /// <returns>The unmasked filename.</returns>
        public string Resolve(string filename)
        {
            var resolved = filename;

            // a well-formed path will consist of a mask, either an alias or a local directory, and a fully qualified path to a
            // file. split the requested filename so we can examine the first two segments
            var parts = filename.Split(new[] { '/', '\\' });

            if (parts.Length < 2)
            {
                return resolved;
            }

            // find the share with a matching mask and alias/local directory
            var share = Shares.FirstOrDefault(share => share.Mask == parts[0] && share.Alias == parts[1]);

            if (share == default)
            {
                return resolved;
            }

            return resolved.ReplaceFirst(share.RemotePath, share.LocalPath);
        }

        /// <summary>
        ///     Searches the cache for the specified <paramref name="query"/> and returns the matching files.
        /// </summary>
        /// <param name="query">The query for which to search.</param>
        /// <returns>The matching files.</returns>
        public async Task<IEnumerable<File>> SearchAsync(SearchQuery query)
        {
            // sanitize the query string. there's probably more to it than this.
            var text = query.Query
                .Replace("/", " ")
                .Replace("\\", " ")
                .Replace(":", " ")
                .Replace("\"", " ");

            var sql = $"SELECT * FROM cache WHERE cache MATCH '\"{text.Replace("'", "''")}\"'";

            try
            {
                using var cmd = new SqliteCommand(sql, SQLite);
                var results = new List<string>();
                var reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    results.Add(reader.GetString(0));
                }

                return results.Select(r => Files[r.Replace("''", "'")]);
            }
            catch (Exception ex)
            {
                // temporary error trap to refine substitution rules
                Console.WriteLine($"[MALFORMED QUERY]: {query} ({ex.Message})");
                return Enumerable.Empty<File>();
            }
        }

        private void CreateTable()
        {
            if (SQLite != null)
            {
                SQLite.Dispose();
            }

            SQLite = new SqliteConnection("Data Source=:memory:");
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
            UnmaskedDirectories = new HashSet<string>();
            MaskedDirectories = new HashSet<string>();
            Files = new Dictionary<string, File>();
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

                    if (string.IsNullOrWhiteSpace(Alias))
                    {
                        throw new ShareAliasException($"Share aliases must not be null, empty or contain only whitespace (provided: {Alias})");
                    }
                    else if (Alias.Contains('\\') || Alias.Contains('/'))
                    {
                        throw new ShareAliasException($"Share aliases must not contain path separators '/' or '\\' (provided: {Alias})");
                    }

                    LocalPath = groups[2].Value;
                }
                else
                {
                    Alias = new Uri(share).Segments.Last();
                    LocalPath = share;
                }

                Mask = Compute.MaskHash(System.IO.Directory.GetParent(LocalPath).FullName);

                var maskedPath = LocalPath.ReplaceFirst(System.IO.Directory.GetParent(LocalPath).FullName, Mask);

                if (!string.IsNullOrEmpty(Alias))
                {
                    var aliasedSegment = LocalPath[(System.IO.Directory.GetParent(LocalPath).FullName.Length + 1)..];
                    RemotePath = maskedPath.ReplaceFirst(aliasedSegment, Alias);
                }
                else
                {
                    RemotePath = LocalPath.ReplaceFirst(System.IO.Directory.GetParent(LocalPath).FullName, Mask);
                }
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
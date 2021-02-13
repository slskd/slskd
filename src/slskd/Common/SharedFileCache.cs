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

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Microsoft.Data.Sqlite;
    using Soulseek;

    /// <summary>
    ///     Caches shared files.
    /// </summary>
    public class SharedFileCache : ISharedFileCache
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharedFileCache"/> class.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="ttl"></param>
        public SharedFileCache(string directory, long ttl)
        {
            Directory = directory;
            TTL = ttl;
        }

        public string Directory { get; }
        private Dictionary<string, Soulseek.File> Files { get; set; }
        public DateTime? LastFill { get; set; }
        private SqliteConnection SQLite { get; set; }
        private ReaderWriterLockSlim SyncRoot { get; } = new ReaderWriterLockSlim();
        public long TTL { get; }

        /// <summary>
        ///     Scans the configured <see cref="Directory"/> and fills the cache.
        /// </summary>
        public void Fill()
        {
            var sw = new Stopwatch();
            sw.Start();

            Console.WriteLine($"[SHARED FILE CACHE]: Refreshing...");

            SyncRoot.EnterWriteLock();

            try
            {
                CreateTable();

                Files = System.IO.Directory.GetFiles(Directory, "*", SearchOption.AllDirectories)
                    .Select(f => new Soulseek.File(1, f, new FileInfo(f).Length, Path.GetExtension(f)))
                    .ToDictionary(f => f.Filename, f => f);

                // potentially optimize with multi-valued insert
                // https://stackoverflow.com/questions/16055566/insert-multiple-rows-in-sqlite
                foreach (var file in Files)
                {
                    InsertFilename(file.Key);
                }
            }
            finally
            {
                SyncRoot.ExitWriteLock();
            }

            sw.Stop();

            Console.WriteLine($"[SHARED FILE CACHE]: Refreshed in {sw.ElapsedMilliseconds}ms.  Found {Files.Count} files.");
            LastFill = DateTime.UtcNow;
        }

        /// <summary>
        ///     Searches the cache for files matching the specified <paramref name="query"/>.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public IEnumerable<Soulseek.File> Search(SearchQuery query)
        {
            if (!LastFill.HasValue || LastFill.Value.AddMilliseconds(TTL) < DateTime.UtcNow)
            {
                Fill();
            }

            return QueryTable(query.Query);
        }

        private void CreateTable()
        {
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

        private IEnumerable<Soulseek.File> QueryTable(string text)
        {
            // sanitize the query string. there's probably more to it than this.
            text = text
                .Replace("/", " ")
                .Replace("\\", " ")
                .Replace(":", " ")
                .Replace("\"", " ");

            var query = $"SELECT * FROM cache WHERE cache MATCH '\"{text.Replace("'", "''")}\"'";

            SyncRoot.EnterReadLock();

            try
            {
                using var cmd = new SqliteCommand(query, SQLite);
                var results = new List<string>();
                var reader = cmd.ExecuteReader();

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
                return Enumerable.Empty<Soulseek.File>();
            }
            finally
            {
                SyncRoot.ExitReadLock();
            }
        }
    }
}
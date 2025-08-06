﻿// <copyright file="SqliteShareRepository.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Timers;
    using Microsoft.Data.Sqlite;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Persistent storage of shared files and metadata.
    /// </summary>
    public class SqliteShareRepository : IShareRepository
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SqliteShareRepository"/> class.
        /// </summary>
        /// <param name="connectionString"></param>
        public SqliteShareRepository(string connectionString)
        {
            ConnectionString = connectionString;

            // in-memory databases will be destroyed if at any point the number of connections reaches zero. to prevent this,
            // create a connection and hold it open for the duration of the application. SQLite will destroy this database when
            // the connection that created it closes. since this is the first connection, it *should* keep the db alive. it's
            // possible that this connection will be recycled somehow due to pooling; the app will crash if this happens so i'll
            // know to keep digging.
            KeepaliveConnection = GetConnection(ConnectionString);
            KeepaliveTimer = new Timer(1000)
            {
                AutoReset = true,
                Enabled = false, // enabled later
            };

            KeepaliveTimer.Elapsed += (_, _) => Keepalive();
        }

        /// <summary>
        ///     Gets the connection string for this repository.
        /// </summary>
        public string ConnectionString { get; }

        private bool Disposed { get; set; }
        private SqliteConnection KeepaliveConnection { get; }
        private Timer KeepaliveTimer { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<SqliteShareRepository>();

        /// <summary>
        ///     Backs the current database up to the database at the specified <paramref name="repository"/>.
        /// </summary>
        /// <param name="repository">The destination repository.</param>
        public void BackupTo(IShareRepository repository)
        {
            using var sourceConn = GetConnection(ConnectionString);
            using var backupConn = GetConnection(repository.ConnectionString);
            sourceConn.BackupDatabase(backupConn);

            Log.Debug("Vacuuming backup");
            using var cmd = new SqliteCommand("VACUUM", backupConn);
            cmd.ExecuteNonQuery();
            Log.Debug("Backup vacuumed successfully");
        }

        /// <summary>
        ///     Counts the number of directories in the database.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for counting subdirectories.</param>
        /// <returns>The number of directories.</returns>
        public int CountDirectories(string parentDirectory = null)
        {
            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(parentDirectory))
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM directories;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM directories WHERE name LIKE @prefix || '%'", conn);
                    cmd.Parameters.AddWithValue("prefix", parentDirectory);
                }

                var reader = cmd.ExecuteReader();
                reader.Read();
                return reader.GetInt32(0);
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        /// <summary>
        ///     Counts the number of files in the database.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for counting files in a subdirectory.</param>
        /// <returns>The number of files.</returns>
        public int CountFiles(string parentDirectory = null)
        {
            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(parentDirectory))
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM files;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM files WHERE maskedFilename LIKE @prefix || '%'", conn);
                    cmd.Parameters.AddWithValue("prefix", parentDirectory);
                }

                var reader = cmd.ExecuteReader();
                reader.Read();
                return reader.GetInt32(0);
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        /// <summary>
        ///     Creates a new database.
        /// </summary>
        /// <remarks>
        ///     Creates tables using 'IF NOT EXISTS', so this is idempotent unless 'discardExisting` is specified, in which case
        ///     tables are explicitly dropped prior to creation.
        /// </remarks>
        /// <param name="discardExisting">An optional value that determines whether the existing database should be discarded.</param>
        public void Create(bool discardExisting = false)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("PRAGMA journal_mode=WAL");

            if (discardExisting)
            {
                conn.ExecuteNonQuery("DROP TABLE IF EXISTS version; DROP TABLE IF EXISTS scans; DROP TABLE IF EXISTS directories; DROP TABLE IF EXISTS filenames; DROP TABLE IF EXISTS files;");
            }

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS version (a INTEGER PRIMARY KEY)");

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS scans (timestamp INTEGER PRIMARY KEY, options TEXT NOT NULL, end INTEGER DEFAULT NULL, suspect INTEGER DEFAULT 0);");

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS directories (name TEXT PRIMARY KEY, timestamp INTEGER NOT NULL);");

            conn.ExecuteNonQuery("CREATE VIRTUAL TABLE IF NOT EXISTS filenames USING fts5(maskedFilename);");

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS files " +
                "(maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, " +
                "extension TEXT, attributeJson TEXT NOT NULL, timestamp INTEGER NOT NULL);");
        }

        /// <summary>
        ///     Disposes this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dumps the contents of the database to a file.
        /// </summary>
        /// <param name="filename">The destination file.</param>
        public void DumpTo(string filename)
        {
            using var sourceConn = GetConnection(ConnectionString);

            // very important! don't use pooling for the backup connection or the file will remain locked
            using var backupConn = GetConnection($"Data Source={filename};Pooling=False");
            sourceConn.BackupDatabase(backupConn);
        }

        /// <summary>
        ///     Enable connection keepalive.
        /// </summary>
        /// <param name="enable">A value indicating whether the keepalive logic should be executed.</param>
        public void EnableKeepalive(bool enable) => KeepaliveTimer.Enabled = enable;

        /// <summary>
        ///     Finds the filename of the file matching the specified <paramref name="maskedFilename"/>.
        /// </summary>
        /// <param name="maskedFilename">The fully qualified remote path of the file.</param>
        /// <returns>The filename, if found.</returns>
        public (string Filename, long Size) FindFileInfo(string maskedFilename)
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT originalFilename, size FROM files WHERE maskedFilename = @maskedFilename;", conn);
            cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);

            var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                Log.Debug("Failed to resolve shared file {Filename}", maskedFilename);
                return default;
            }

            var filename = (string)reader["originalFilename"];
            var size = (long)reader["size"];
            Log.Debug($"Resolved requested shared file {maskedFilename} to {filename} ({size} bytes)");
            return (filename, size);
        }

        /// <summary>
        ///     Finds and returns the most recent scan record.
        /// </summary>
        /// <returns>The most recent scan record, or default if no scan was found.</returns>
        public Scan FindLatestScan()
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT timestamp, options, COALESCE(end, -1), suspect FROM scans ORDER BY timestamp DESC LIMIT 1", conn);

            var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return default;
            }

            var timestamp = reader.GetInt64(0);
            var optionsJson = reader.GetString(1);

            var endRaw = reader.GetInt64(2);
            long? end = endRaw == -1 ? null : endRaw;

            var suspect = reader.GetInt32(3) > 0;

            return new Scan { StartedAt = timestamp, OptionsJson = optionsJson, EndedAt = end, Suspect = suspect };
        }

        /// <summary>
        ///     Flags the latest scan as suspect, indicating that the cached contents may have diverged from physical storage.
        /// </summary>
        public void FlagLatestScanAsSuspect()
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("UPDATE scans SET suspect = 1 WHERE timestamp = (SELECT timestamp FROM scans ORDER BY timestamp DESC LIMIT 1)", conn);

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Inserts a directory.
        /// </summary>
        /// <param name="name">The fully qualified local name of the directory.</param>
        /// <param name="timestamp">The timestamp to assign to the record.</param>
        public void InsertDirectory(string name, long timestamp)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO directories VALUES(@name, @timestamp) ON CONFLICT DO UPDATE SET timestamp = excluded.timestamp;", cmd =>
            {
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("timestamp", timestamp);
            });
        }

        /// <summary>
        ///     Inserts a file.
        /// </summary>
        /// <param name="maskedFilename">The fully qualified remote path of the file.</param>
        /// <param name="originalFilename">The fully qualified local path of the file.</param>
        /// <param name="touchedAt">The timestamp at which the file was last modified, according to the host OS.</param>
        /// <param name="file">The Soulseek.File instance representing the file.</param>
        /// <param name="timestamp">The timestamp to assign to the record.</param>
        public void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, Soulseek.File file, long timestamp)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO files (maskedFilename, originalFilename, size, touchedAt, code, extension, attributeJson, timestamp) " +
                "VALUES(@maskedFilename, @originalFilename, @size, @touchedAt, @code, @extension, @attributeJson, @timestamp) " +
                "ON CONFLICT DO UPDATE SET originalFilename = excluded.originalFilename, size = excluded.size, touchedAt = excluded.touchedAt, " +
                "code = excluded.code, extension = excluded.extension, attributeJson = excluded.attributeJson, timestamp = excluded.timestamp;", cmd =>
                {
                    cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);
                    cmd.Parameters.AddWithValue("originalFilename", originalFilename);
                    cmd.Parameters.AddWithValue("size", file.Size);
                    cmd.Parameters.AddWithValue("touchedAt", touchedAt.ToLongDateString());
                    cmd.Parameters.AddWithValue("code", file.Code);
                    cmd.Parameters.AddWithValue("extension", file.Extension);
                    cmd.Parameters.AddWithValue("attributeJson", file.Attributes.ToJson());
                    cmd.Parameters.AddWithValue("timestamp", timestamp);
                });
        }

        /// <summary>
        ///     Inserts a scan record at the specified <paramref name="timestamp"/>.
        /// </summary>
        /// <param name="timestamp">The timestamp associated with the scan.</param>
        /// <param name="options">The options snapshot at the start of the scan.</param>
        public void InsertScan(long timestamp, Options.SharesOptions options)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO scans (timestamp, options) VALUES(@timestamp, @options)", cmd =>
            {
                cmd.Parameters.AddWithValue("timestamp", timestamp);
                cmd.Parameters.AddWithValue("options", options.ToJson());
            });
        }

        /// <summary>
        ///     Lists all directories.
        /// </summary>
        /// <param name="parentDirectory">The optional directory prefix used for listing subdirectories.</param>
        /// <returns>The list of directories.</returns>
        public IEnumerable<string> ListDirectories(string parentDirectory = null)
        {
            var results = new List<string>();

            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(parentDirectory))
                {
                    cmd = new SqliteCommand("SELECT name FROM directories ORDER BY name ASC;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT name from directories WHERE name LIKE @prefix || '%' ORDER BY name ASC;", conn);
                    cmd.Parameters.AddWithValue("prefix", parentDirectory);
                }

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    results.Add(reader.GetString(0));
                }

                return results;
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        /// <summary>
        ///     Lists all files.
        /// </summary>
        /// <param name="parentDirectory">The optional parent directory.</param>
        /// <param name="includeFullPath">A value indicating whether the fully qualified path should be returned.</param>
        /// <returns>The list of files.</returns>
        public IEnumerable<Soulseek.File> ListFiles(string parentDirectory = null, bool includeFullPath = false)
        {
            var results = new List<Soulseek.File>();

            SqliteCommand cmd = default;
            using var conn = GetConnection();

            try
            {
                if (string.IsNullOrEmpty(parentDirectory))
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files ORDER BY maskedFilename ASC;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT maskedFilename, code, size, extension, attributeJson FROM files WHERE maskedFilename LIKE @match ORDER BY maskedFilename ASC;", conn);
                    cmd.Parameters.AddWithValue("match", parentDirectory + '%');
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

                    filename = includeFullPath ? filename : filename.GetNormalizedFileName();

                    var file = new Soulseek.File(code, filename, size, extension, attributeList);

                    results.Add(file);
                }

                return results;
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        /// <summary>
        ///     Returns the list of all <see cref="Scan"/> started at or after the specified <paramref name="startedAtOrAfter"/>
        ///     unix timestamp.
        /// </summary>
        /// <param name="startedAtOrAfter">A unix timestamp that serves as the lower bound of the time-based listing.</param>
        /// <returns>The operation context, including the list of found scans.</returns>
        public IEnumerable<Scan> ListScans(long startedAtOrAfter = 0)
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand($"SELECT timestamp, options, COALESCE(end, -1), suspect FROM scans WHERE timestamp >= {startedAtOrAfter}", conn);

            var reader = cmd.ExecuteReader();
            var scans = new List<Scan>();

            while (reader.Read())
            {
                var timestamp = reader.GetInt64(0);
                var optionsJson = reader.GetString(1);

                var endRaw = reader.GetInt64(2);
                long? end = endRaw == -1 ? null : endRaw;

                var suspect = reader.GetInt32(3) > 0;

                scans.Add(new Scan { StartedAt = timestamp, OptionsJson = optionsJson, EndedAt = end, Suspect = suspect });
            }

            return scans;
        }

        /// <summary>
        ///     Deletes directory records with a timestamp prior to the specified <paramref name="olderThanTimestamp"/>.
        /// </summary>
        /// <param name="olderThanTimestamp">The timestamp before which to delete directories.</param>
        /// <returns>The number of records deleted.</returns>
        public long PruneDirectories(long olderThanTimestamp)
        {
            using var conn = GetConnection();

            using var cmd = new SqliteCommand("DELETE FROM directories WHERE timestamp < @timestamp; SELECT changes()", conn);
            cmd.Parameters.AddWithValue("timestamp", olderThanTimestamp);

            var reader = cmd.ExecuteReader();
            reader.Read();

            return reader.GetInt64(0);
        }

        /// <summary>
        ///     Deletes file records with a timestamp prior to the specified <paramref name="olderThanTimestamp"/>.
        /// </summary>
        /// <param name="olderThanTimestamp">The timestamp before which to delete files.</param>
        /// <returns>The number of records deleted.</returns>
        public long PruneFiles(long olderThanTimestamp)
        {
            using var conn = GetConnection();

            using var cmd = new SqliteCommand("DELETE FROM files WHERE timestamp < @timestamp; SELECT changes()", conn);
            cmd.Parameters.AddWithValue("timestamp", olderThanTimestamp);

            var reader = cmd.ExecuteReader();
            reader.Read();

            return reader.GetInt64(0);
        }

        /// <summary>
        ///     Rebuilds the filename index table using the data in the files table.
        /// </summary>
        public void RebuildFilenameIndex()
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("DELETE from filenames; INSERT INTO filenames SELECT maskedFilename FROM files;");
        }

        /// <summary>
        ///     Restores the current database from the database at the specified <paramref name="repository"/>.
        /// </summary>
        /// <param name="repository">The destination repository.</param>
        public void RestoreFrom(IShareRepository repository)
        {
            using var sourceConn = GetConnection(repository.ConnectionString);
            using var restoreConn = GetConnection();
            sourceConn.BackupDatabase(restoreConn);
        }

        /// <summary>
        ///     Searches the database for files matching the specified <paramref name="query"/>.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>The list of matching files.</returns>
        public IEnumerable<Soulseek.File> Search(SearchQuery query)
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

            var results = new List<Soulseek.File>();

            try
            {
                using var conn = GetConnection();
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var filename = reader.GetString(0);

                    // SQLite takes care of matching on tokens, but unfortunately doesn't work in cases where an exclusion appears as a substring.
                    // example: the token matching works to exclude the result "foo bar baz" given a "bar" exclusion, but does not exclude "foo xbarx baz"
                    // for this reason we must iterate over the exclusion list and ensure none appear as substrings
                    // **CAUTION** performance of this will degrade exponentially as the list of exclusions grows
                    if (query.Exclusions.Any(x => filename.Contains(x, StringComparison.OrdinalIgnoreCase)))
                    {
                        Log.Debug("Dropping excluded filename {File}", filename);
                        continue;
                    }

                    var code = reader.GetInt32(1);
                    var size = reader.GetInt64(2);
                    var extension = reader.GetString(3);
                    var attributeJson = reader.GetString(4);

                    var attributeList = attributeJson.FromJson<List<FileAttribute>>();

                    var file = new Soulseek.File(code, filename, size, extension, attributeList);
                    results.Add(file);
                }

                return results;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to execute shared file query '{Query}': {Message}", query, ex.Message);
                return Enumerable.Empty<Soulseek.File>();
            }
        }

        /// <summary>
        ///     Attempts to validate the backing database.
        /// </summary>
        /// <returns>A value indicating whether the database is valid.</returns>
        public bool TryValidate()
        {
            return TryValidate(out _);
        }

        /// <summary>
        ///     Attempts to validate the backing database.
        /// </summary>
        /// <param name="problems">The list of problems, if the database is invalid.</param>
        /// <returns>A value indicating whether the database is valid.</returns>
        public bool TryValidate(out IEnumerable<string> problems)
        {
            var list = new List<string>();
            problems = list;

            // to update this schema map, run the following query against a valid, up-to-date database and paste the output below:
            // select '{ "' || name || '", "' || sql || '" },' from sqlite_master where type = 'table'
            var schema = new Dictionary<string, string>()
            {
                { "version", "CREATE TABLE version (a INTEGER PRIMARY KEY)" },
                { "scans", "CREATE TABLE scans (timestamp INTEGER PRIMARY KEY, options TEXT NOT NULL, end INTEGER DEFAULT NULL, suspect INTEGER DEFAULT 0)" },
                { "directories", "CREATE TABLE directories (name TEXT PRIMARY KEY, timestamp INTEGER NOT NULL)" },
                { "filenames", "CREATE VIRTUAL TABLE filenames USING fts5(maskedFilename)" },
                { "filenames_data", "CREATE TABLE 'filenames_data'(id INTEGER PRIMARY KEY, block BLOB)" },
                { "filenames_idx", "CREATE TABLE 'filenames_idx'(segid, term, pgno, PRIMARY KEY(segid, term)) WITHOUT ROWID" },
                { "filenames_content", "CREATE TABLE 'filenames_content'(id INTEGER PRIMARY KEY, c0)" },
                { "filenames_docsize", "CREATE TABLE 'filenames_docsize'(id INTEGER PRIMARY KEY, sz BLOB)" },
                { "filenames_config", "CREATE TABLE 'filenames_config'(k PRIMARY KEY, v) WITHOUT ROWID" },
                { "files", "CREATE TABLE files (maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, extension TEXT, attributeJson TEXT NOT NULL, timestamp INTEGER NOT NULL)" },
            };

            try
            {
                Log.Debug("Validating shares database with connection string {String}", ConnectionString);

                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();

                using var cmd = new SqliteCommand("SELECT name, sql from sqlite_master WHERE type = 'table';", conn);

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
                            list.Add($"Expected {table} schema to be {expectedSql}, found {actualSql}");
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
                    list.Add($"Expected {schema.Count} tables, found {rows}");
                }

                return !problems.Any();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, $"Failed to validate database: {ex.Message}");
                list.Add($"Failed to validate database: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Updates the scan started at the specified <paramref name="timestamp"/> to set the <paramref name="end"/>.
        /// </summary>
        /// <param name="timestamp">The timestamp associated with the scan.</param>
        /// <param name="end">The timestamp at the conclusion of the scan.</param>
        public void UpdateScan(long timestamp, long end)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("UPDATE scans SET end = @end WHERE timestamp = @timestamp;", cmd =>
            {
                cmd.Parameters.AddWithValue("end", end);
                cmd.Parameters.AddWithValue("timestamp", timestamp);
            });
        }

        /// <summary>
        ///     Reclaims unused space.
        /// </summary>
        public void Vacuum()
        {
            using var conn = GetConnection();
            conn.ExecuteNonQuery("VACUUM;");
        }

        /// <summary>
        ///     Disposes this object.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    KeepaliveConnection.Dispose();
                }

                Disposed = true;
            }
        }

        private SqliteConnection GetConnection(string connectionString = null)
        {
            connectionString ??= ConnectionString;

            var conn = new SqliteConnection(connectionString);
            conn.Open();
            return conn;
        }

        private void Keepalive()
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM pragma_table_info(\"filenames\");", KeepaliveConnection);

            var reader = cmd.ExecuteReader();

            if (!reader.Read() || reader.GetInt32(0) != 1)
            {
                var msg = "The internal share database has been corrupted or lost, and the application cannot continue to run. Please report this in a GitHub issue here: https://github.com/sredevopsorg/slskd/issues";
                Log.Fatal(msg);
                Environment.Exit(1);
                throw new DataMisalignedException(msg);
            }
        }
    }
}
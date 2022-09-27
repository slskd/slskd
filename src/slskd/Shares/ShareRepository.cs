// <copyright file="ShareRepository.cs" company="slskd Team">
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
    using System.IO;
    using Microsoft.Data.Sqlite;
    using Serilog;
    using Soulseek;

    public interface IShareRepository
    {
        int CountDirectories(string prefix = null);
        int CountFiles(string prefix = null);
        void CreateTables(bool discardExisting = false);
        bool TryValidateTables(string connectionString);
        void InsertDirectory(string name, long timestamp);
        void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, Soulseek.File file, long timestamp);
        long DeleteDirectories(long olderThanTimestamp);
        long DeleteFiles(long olderThanTimestamp);
        IEnumerable<string> ListDirectories(string prefix = null);
        IEnumerable<Soulseek.File> ListFiles(string directory = null, bool includeFullPath = false);
        void Backup(string backupConnectionString);
        string FindOriginalFilename(string maskedFilename);
    }

    public class ShareRepository : IShareRepository
    {
        public ShareRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        private string ConnectionString { get; }

        private SqliteConnection GetConnection(string connectionString = null)
        {
            connectionString ??= ConnectionString;

            var conn = new SqliteConnection(connectionString);
            conn.Open();
            return conn;
        }

        public string FindOriginalFilename(string maskedFilename)
        {
            using var conn = GetConnection();
            using var cmd = new SqliteCommand("SELECT originalFilename FROM files WHERE maskedFilename = @maskedFilename;", conn);
            cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);

            var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                Log.Warning("Failed to resolve shared file {Filename}", maskedFilename);
                return null;
            }

            var resolved = reader.GetString(0);
            Log.Debug($"Resolved requested shared file {maskedFilename} to {resolved}");
            return resolved;
        }

        public void CreateTables(bool discardExisting = false)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("PRAGMA journal_mode=WAL");

            if (discardExisting)
            {
                conn.ExecuteNonQuery("DROP TABLE IF EXISTS directories; DROP TABLE IF EXISTS filenames; DROP TABLE IF EXISTS files;");
            }

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS directories (name TEXT PRIMARY KEY, timestamp INTEGER NOT NULL);");

            conn.ExecuteNonQuery("CREATE VIRTUAL TABLE IF NOT EXISTS filenames USING fts5(maskedFilename);");

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS files " +
                "(maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, " +
                "extension TEXT, attributeJson TEXT NOT NULL, timestamp INTEGER NOT NULL);");
        }

        public bool TryValidateTables(string connectionString)
        {
            var schema = new Dictionary<string, string>()
            {
                { "directories", "CREATE TABLE directories (name TEXT PRIMARY KEY, timestamp INTEGER NOT NULL)" },
                { "filenames", "CREATE VIRTUAL TABLE filenames USING fts5(maskedFilename)" },
                { "files", "CREATE TABLE files (maskedFilename TEXT PRIMARY KEY, originalFilename TEXT NOT NULL, size BIGINT NOT NULL, touchedAt TEXT NOT NULL, code INTEGER DEFAULT 1 NOT NULL, extension TEXT, attributeJson TEXT NOT NULL, timestamp INTEGER NOT NULL)" },
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

        public void Backup(string backupConnectionString)
        {
            using var sourceConn = GetConnection(ConnectionString);
            using var backupConn = GetConnection(backupConnectionString);
            sourceConn.BackupDatabase(backupConn);
        }

        public int CountDirectories(string prefix = null)
        {
            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM directories;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM directories WHERE name LIKE @prefix || '%'", conn);
                    cmd.Parameters.AddWithValue("prefix", prefix);
                }

                var reader = cmd.ExecuteReader();
                reader.Read();
                return reader.GetInt32(0);
            }
            finally
            {
                cmd.Dispose();
            }
        }

        public int CountFiles(string prefix = null)
        {
            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM files;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT COUNT(*) FROM files WHERE maskedFilename LIKE @prefix || '%'", conn);
                    cmd.Parameters.AddWithValue("prefix", prefix);
                }

                var reader = cmd.ExecuteReader();
                reader.Read();
                return reader.GetInt32(0);
            }
            finally
            {
                cmd.Dispose();
            }
        }

        public void InsertDirectory(string name, long timestamp)
        {
            using var conn = GetConnection();

            conn.ExecuteNonQuery("INSERT INTO directories VALUES(@name, @timestamp) ON CONFLICT DO UPDATE SET timestamp = excluded.timestamp;", cmd =>
            {
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("timestamp", timestamp);
            });
        }

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

            conn.ExecuteNonQuery("INSERT INTO filenames (maskedFilename) VALUES(@maskedFilename);", cmd =>
            {
                cmd.Parameters.AddWithValue("maskedFilename", maskedFilename);
            });
        }

        public long DeleteDirectories(long olderThanTimestamp)
        {
            using var conn = GetConnection();

            using var cmd = new SqliteCommand("DELETE FROM directories WHERE timestamp < @timestamp; SELECT changes()", conn);
            cmd.Parameters.AddWithValue("timestamp", olderThanTimestamp);

            var reader = cmd.ExecuteReader();
            reader.Read();

            return reader.GetInt64(0);
        }

        public long DeleteFiles(long olderThanTimestamp)
        {
            using var conn = GetConnection();

            using var cmd = new SqliteCommand("DELETE FROM files WHERE timestamp < @timestamp; SELECT changes()", conn);
            cmd.Parameters.AddWithValue("timestamp", olderThanTimestamp);

            var reader = cmd.ExecuteReader();
            reader.Read();

            return reader.GetInt64(0);
        }

        public IEnumerable<string> ListDirectories(string prefix = null)
        {
            var results = new List<string>();

            using var conn = GetConnection();

            SqliteCommand cmd = default;

            try
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    cmd = new SqliteCommand("SELECT name FROM directories ORDER BY name ASC;", conn);
                }
                else
                {
                    cmd = new SqliteCommand("SELECT name from directories WHERE name LIKE @prefix || '%' ORDER BY name ASC;", conn);
                    cmd.Parameters.AddWithValue("prefix", prefix);
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
                cmd.Dispose();
            }
        }

        public IEnumerable<Soulseek.File> ListFiles(string directory = null, bool includeFullPath = false)
        {
            var results = new List<Soulseek.File>();

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

                    filename = includeFullPath ? filename : Path.GetFileName(filename);

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
    }
}

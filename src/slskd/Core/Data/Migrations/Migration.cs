// <copyright file="Migration.cs" company="slskd Team">
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

namespace slskd.Migrations;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

public record ColumnInfo(long Cid, string Name, string Type, bool NotNull, object DefaultValue, bool PrimaryKey);

public abstract class Migration
{
    public Migration(string databaseName)
    {
        DatabaseName = databaseName;
    }

    public string DatabaseName { get; }
    public string DatabaseFileName => Path.Combine(Program.DataDirectory, DatabaseName);
    public string ConnectionString => $"Data Source={DatabaseFileName}.db";

    public abstract void Apply();

    protected Dictionary<string, IEnumerable<ColumnInfo>> GetDatabaseSchema()
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var dict = new Dictionary<string, IEnumerable<ColumnInfo>>();

            using var tableCommand = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection);
            using var tableReader = tableCommand.ExecuteReader();

            while (tableReader.Read())
            {
                var table = tableReader.GetString(0);

                var columns = new List<ColumnInfo>();

                using var columnCommand = new SqliteCommand($"PRAGMA table_info({table});", connection);
                using var cr = columnCommand.ExecuteReader();

                while (cr.Read())
                {
                    columns.Add(new ColumnInfo(
                        Cid: cr.GetInt64(cr.GetOrdinal("cid")),
                        Name: cr.GetString(cr.GetOrdinal("name")),
                        Type: cr.GetString(cr.GetOrdinal("type")),
                        NotNull: cr.GetInt64(cr.GetOrdinal("notnull")) > 0,
                        DefaultValue: cr["dflt_value"],
                        PrimaryKey: cr.GetInt64(cr.GetOrdinal("pk")) > 0));
                }

                dict[table] = columns;
            }

            return dict;
        }
        catch (Exception ex)
        {
            throw new SlskdException($"Failed to retrieve schema information for database '{DatabaseName}'. The database might be corrupt or in use by another application; if the problem persists the file ({DatabaseFileName}) may need to be deleted", ex);
        }
    }
}
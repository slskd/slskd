// <copyright file="Sqlite.cs" company="slskd Team">
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
    using Microsoft.Data.Sqlite;

    public static class Sqlite
    {
        public static void Backup(string sourceConnectionString, string destinationConnectionString)
        {
            using var source = new SqliteConnection(sourceConnectionString);
            using var destination = new SqliteConnection(destinationConnectionString);

            source.Open();
            destination.Open();

            source.BackupDatabase(destination);
        }

        public static void Restore(string sourceConnectionString, string destinationConnectionString)
            => Backup(sourceConnectionString, destinationConnectionString);

        public static int ExecuteNonQuery(this SqliteConnection conn, string query, Action<SqliteCommand> action = null)
        {
            using var cmd = new SqliteCommand(query, conn);
            action?.Invoke(cmd);
            return cmd.ExecuteNonQuery();
        }
    }
}

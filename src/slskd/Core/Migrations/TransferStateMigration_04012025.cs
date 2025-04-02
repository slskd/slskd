// <copyright file="TransferStateMigration.cs" company="slskd Team">
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
using System.IO;
using Microsoft.Data.Sqlite;

public class TransferStateMigration_04012025 : IMigration
{
    public void Apply()
    {

    }

    static void AddStateDescriptionColumn(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            Console.WriteLine("Database file not found.");
            return;
        }

        string connectionString = $"Data Source={dbPath};Version=3;";

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Check if the column exists
            bool columnExists = false;
            using (var command = new SqliteCommand("PRAGMA table_info(Transfers);", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader["name"].ToString();
                    if (columnName.Equals("StateDescription", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (columnExists)
            {
                Console.WriteLine("Column 'StateDescription' already exists.");
                return;
            }

            Console.WriteLine("Adding 'StateDescription' column...");

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Rename old table
                    using (var renameCommand = new SqliteCommand("ALTER TABLE Transfers RENAME TO Transfers_old;", connection))
                    {
                        renameCommand.ExecuteNonQuery();
                    }

                    // Create new table with StateDescription after State
                    string createNewTableSQL = @"
                    CREATE TABLE Transfers (
                        Id TEXT NOT NULL,
                        Username TEXT,
                        Direction TEXT NOT NULL,
                        Filename TEXT,
                        Size INTEGER NOT NULL,
                        StartOffset INTEGER NOT NULL,
                        State TEXT NOT NULL,
                        StateDescription TEXT, -- New column
                        RequestedAt TEXT NOT NULL,
                        EnqueuedAt TEXT,
                        StartedAt TEXT,
                        EndedAt TEXT,
                        BytesTransferred INTEGER NOT NULL,
                        AverageSpeed REAL NOT NULL,
                        PlaceInQueue INTEGER,
                        Exception TEXT,
                        Removed INTEGER NOT NULL,
                        CONSTRAINT PK_Transfers PRIMARY KEY(Id)
                    );";

                    using (var createCommand = new SqliteCommand(createNewTableSQL, connection))
                    {
                        createCommand.ExecuteNonQuery();
                    }

                    // Copy data into the new table
                    string copyDataSQL = @"
                    INSERT INTO Transfers (
                        Id, Username, Direction, Filename, Size, StartOffset, State, 
                        RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, 
                        AverageSpeed, PlaceInQueue, Exception, Removed
                    )
                    SELECT 
                        Id, Username, Direction, Filename, Size, StartOffset, State, 
                        RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, 
                        AverageSpeed, PlaceInQueue, Exception, Removed
                    FROM Transfers_old;";

                    using (var copyCommand = new SqliteCommand(copyDataSQL, connection))
                    {
                        copyCommand.ExecuteNonQuery();
                    }

                    // Drop the old table
                    using (var dropCommand = new SqliteCommand("DROP TABLE Transfers_old;", connection))
                    {
                        dropCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    Console.WriteLine("Column 'StateDescription' added successfully.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            connection.Close();
        }
    }
}

// <copyright file="TransferStateMigration_04012025.cs" company="slskd Team">
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
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Serilog;
using Soulseek;

/// <summary>
///     Updates the Transfers table to:
///
///     * Add a new StatusDescription (TEXT) column
///     * Copy the current (string) contents of the Status column to StatusDescription
///     * Change the type of the Status column to INTEGER
///     * Set the contents of the Status column to the numeric representation of the existing Status
///
///     This is necessary because Entity Framework doesn't work with [Flags] enums that are stored
///     as strings; it tries to use bitwise operators to apply HasFlags(), and these obviously don't
///     work against strings. Not sure why EF didn't complain about this, but here we are.
/// </summary>
public class TransferStateMigration_04012025 : Migration
{
    public TransferStateMigration_04012025(string databasName)
        : base(databasName)
    {
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<TransferStateMigration_04012025>();

    public override void Apply()
    {
        // first, check the existing database to see if the StatusDescription column exists
        // if it does, the migration has already been applied
        var schema = GetDatabaseSchema();
        var txfers = schema["Transfers"];

        if (txfers.Any(c => c.Name == "StatusDescription"))
        {
            Log.Information("Migration {Name} has already been applied", nameof(TransferStateMigration_04012025));
            return;
        }

        using var connection = new SqliteConnection($"Data Source={Path.Combine(Program.DataDirectory, "transfers.db")}");
        connection.Open();

        // get a distinct list of states from the existing data and translate each into the corresponding integer
        var stateMapping = GetStateMapping(connection);

        AddStateDescriptionColumn(connection, stateMapping);
    }

    private Dictionary<string, int> GetStateMapping(SqliteConnection connection)
    {
        Log.Debug("Fetching states and mapping to integers...");

        using var command = new SqliteCommand("SELECT DISTINCT State FROM Transfers;", connection);
        using var reader = command.ExecuteReader();

        var dict = new Dictionary<string, int>();

        while (reader.Read())
        {
            var state = reader.GetString(0);
            dict[state] = (int)Enum.Parse(typeof(TransferStates), state);
        }

        Log.Debug("State -> int map: {Map}", dict);
        return dict;
    }

    private void AddStateDescriptionColumn(SqliteConnection connection, Dictionary<string, int> stateMapping)
    {
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                // rename old table
                var renameCommand = new SqliteCommand("ALTER TABLE Transfers RENAME TO Transfers_old;", connection, transaction))
                renameCommand.ExecuteNonQuery();

                // create new table with StateDescription after State
                var createCommand = new SqliteCommand(@"
                    CREATE TABLE Transfers (
                        Id TEXT NOT NULL,
                        Username TEXT,
                        Direction TEXT NOT NULL,
                        Filename TEXT,
                        Size INTEGER NOT NULL,
                        StartOffset INTEGER NOT NULL,
                        State INTEGER NOT NULL,
                        StateDescription TEXT, -- new column
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
                    );", connection, transaction);
                createCommand.ExecuteNonQuery();

                // copy the old data into the new table
                var copyCommand = new SqliteCommand(@"
                    INSERT INTO Transfers (
                        Id, Username, Direction, Filename, Size, StartOffset, State, 
                        RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, 
                        AverageSpeed, PlaceInQueue, Exception, Removed
                    )
                    SELECT 
                        Id, Username, Direction, Filename, Size, StartOffset, State, 
                        RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, 
                        AverageSpeed, PlaceInQueue, Exception, Removed
                    FROM Transfers_old;",
                connection, transaction);
                copyCommand.ExecuteNonQuery();

                // copy the status from Status to StatusDescription
                var copyColumnCommand = new SqliteCommand("UPDATE Transfers SET StateDescription = State", connection, transaction);
                copyColumnCommand.ExecuteNonQuery();

                // todo: set Status to the integer representation of the column
                foreach (var state in stateMapping)
                {
                    Log.Debug("Set {String} to {Int}", state.Key, state.Value);

                    var mapCommand = new SqliteCommand($"UPDATE Transfers SET State = {state.Value} WHERE State = {state.Key}");
                    mapCommand.ExecuteNonQuery();
                }

                // Drop the old table
                using (var dropCommand = new SqliteCommand("DROP TABLE Transfers_old;", connection, transaction))
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
    }
}

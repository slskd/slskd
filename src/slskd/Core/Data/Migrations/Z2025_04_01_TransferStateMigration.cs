// <copyright file="Z2025_04_01_TransferStateMigration.cs" company="slskd Team">
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
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.Sqlite;
using Serilog;
using slskd.Transfers;

/// <summary>
///     Updates the Transfers table to:
///
///     * Add a new StateDescription (TEXT) column
///     * Copy the current (string) contents of the State column to StateDescription
///     * Change the type of the State column to INTEGER
///     * Set the contents of the State column to the numeric representation of the existing State
///
///     This is necessary because Entity Framework doesn't work with [Flags] enums that are stored
///     as strings; it tries to use bitwise operators to apply HasFlags(), and these obviously don't
///     work against strings. Not sure why EF didn't complain about this, but here we are.
/// </summary>
public class Z2025_04_01_TransferStateMigration : IMigration
{
    public Z2025_04_01_TransferStateMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Transfers];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z2025_04_01_TransferStateMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        // first, check the existing database to see if the StateDescription column exists
        // if it does, the migration has already been applied, or the database has been recreated
        var schema = SchemaInspector.GetDatabaseSchema(ConnectionString);
        var txfers = schema["Transfers"];

        return !txfers.Any(c => c.Name == nameof(Transfer.StateDescription));
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z2025_04_01_TransferStateMigration));
            return;
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        // get a distinct list of states from the existing data and translate each into the corresponding integer
        var stateMapping = GetStateMapping(connection);

        ModifyTableAndMassageData(connection, stateMapping);
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
            dict[state] = (int)Enum.Parse(typeof(Z04012025_TransferStateMigration_TransferStates), state);
        }

        Log.Debug("State -> int map: {Map}", dict);
        return dict;
    }

    private void ModifyTableAndMassageData(SqliteConnection connection, Dictionary<string, int> stateMapping)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            /*
                rename old table so we can create the new one with the layout we want
                while preserving the existing data
            */
            var rename = @"
                ALTER TABLE Transfers RENAME TO Transfers_old;
            ";

            using var renameCommand = new SqliteCommand(rename, connection, transaction);
            renameCommand.ExecuteNonQuery();

            /*
                create the new table with StateDescription appearing after State
            */
            var create = @"
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
                );
            ";

            using var createCommand = new SqliteCommand(create, connection, transaction);
            createCommand.ExecuteNonQuery();

            Log.Information("> Schema changes applied, copying data...");

            /*
                copy the existing data into the new table
            */
            var copy = @"
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

            using var copyCommand = new SqliteCommand(copy, connection, transaction);
            copyCommand.ExecuteNonQuery();

            Log.Information("> Data copied, adjusting columns...");

            /*
                we no longer need the old table, so drop it
            */
            using var dropCommand = new SqliteCommand("DROP TABLE Transfers_old;", connection, transaction);
            dropCommand.ExecuteNonQuery();

            /*
                copy the existing (string) State value from State to StateDescription
            */
            var copyColumnCommand = new SqliteCommand("UPDATE Transfers SET StateDescription = State", connection, transaction);
            copyColumnCommand.ExecuteNonQuery();

            /*
                for each of the distinct State values that exist in the data, convert the string
                representation into the numeric

                there surely exist better ways to do this, but the reality is that 99% of the records
                will be "Completed, Succeeded" (48), with a few "Completed, Errored" or "Completed, Cancelled"
                sprinkled in. this should run in reasonable time for large collections
            */
            var sw = new Stopwatch();
            var step = 1;

            foreach (var state in stateMapping)
            {
                sw.Reset();
                Log.Information("> Updating column {Step} of {Total}. This may take some time...", step++, stateMapping.Count);

                Log.Debug("Setting {String} to {Int}", state.Key, state.Value);

                var mapCommand = new SqliteCommand($"UPDATE Transfers SET State = {state.Value} WHERE State = '{state.Key}';", connection, transaction);
                mapCommand.ExecuteNonQuery();

                Log.Debug("{String} to {Int} in {Duration}", state.Key, state.Value, sw.ElapsedMilliseconds);
            }

            Log.Debug("Committing transation...");
            transaction.Commit();
            Log.Debug("Transaction committed!");
            Log.Information("> Done!");
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    /// <summary>
    ///    This is a copy of the TransferStates enum as it was at the time this migration was written. This exists to ensure
    ///    that the migration can be applied to any version of the database, even if the enum changes in the future.
    /// </summary>
    [Flags]
    private enum Z04012025_TransferStateMigration_TransferStates
    {
        None = 0,
        Requested = 1,
        Queued = 2,
        Initializing = 4,
        InProgress = 8,
        Completed = 16,
        Succeeded = 32,
        Cancelled = 64,
        TimedOut = 128,
        Errored = 256,
        Rejected = 512,
        Aborted = 1024,
        Locally = 2048,
        Remotely = 4096,
    }
#pragma warning restore SA1201 // Elements should appear in the correct order
}

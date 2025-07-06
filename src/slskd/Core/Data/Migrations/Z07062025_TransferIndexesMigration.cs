// <copyright file="Z07062025_TransferIndexesMigration.cs" company="slskd Team">
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
using System.Linq;
using Microsoft.Data.Sqlite;
using Serilog;

/// <summary>
///     Updates the Transfers table to add indexes on the Direction and State columns.
/// </summary>
public class Z07062025_TransferIndexesMigration : IMigration
{
    private ILogger Log { get; } = Serilog.Log.ForContext<Z07062025_TransferIndexesMigration>();
    private string DatabaseName { get; } = "transfers";
    private string ConnectionString => $"Data Source={Path.Combine(Program.DataDirectory, $"{DatabaseName}.db")}";

    public bool NeedsToBeApplied()
    {
        // check to see if *BOTH* of the indexes are in place. if one or both are missing, we must apply
        var schema = SchemaInspector.GetDatabaseIndexes(ConnectionString);
        var txfers = schema["Transfers"];

        var directionExists = txfers.Any(c => c.Name == "IDX_Transfers_Direction");
        var stateExists = txfers.Any(c => c.Name == "IDX_Transfers_State");

        if (directionExists && stateExists)
        {
            return false;
        }

        return true;
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z07062025_TransferIndexesMigration));
            return;
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            Log.Information("> Adding missing index(es) on the Transfers table...");

            var directionCommand = new SqliteCommand(@"
                CREATE INDEX IF NOT EXISTS IDX_Transfers_Direction ON Transfers (Direction)
            ", connection, transaction);
            directionCommand.ExecuteNonQuery();

            var stateCommand = new SqliteCommand(@"
                CREATE INDEX IF NOT EXISTS IDX_Transfers_State ON Transfers (State)
            ", connection, transaction);
            stateCommand.ExecuteNonQuery();

            Log.Information("> Index(es) created");
            transaction.Commit();
            Log.Information("> Done!");
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }
}

// <copyright file="Z2025_07_26_PrivateMessageWasReplayedMigration.cs" company="slskd Team">
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
using System.Linq;
using Microsoft.Data.Sqlite;
using Serilog;
using slskd.Messaging;

/// <summary>
///     Updates the Transfers table to add indexes on the Direction and State columns.
/// </summary>
public class Z2025_07_26_PrivateMessageWasReplayedMigration : IMigration
{
    public Z2025_07_26_PrivateMessageWasReplayedMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Messaging];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z2025_07_26_PrivateMessageWasReplayedMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        var schema = SchemaInspector.GetDatabaseSchema(ConnectionString);
        var pms = schema["PrivateMessages"];

        if (pms.Any(r => r.Name == nameof(PrivateMessage.WasReplayed)))
        {
            return false;
        }

        return true;
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z2025_07_26_PrivateMessageWasReplayedMigration));
            return;
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            Log.Information("> Adding missing column WasReplayed to PrivateMessages table...");

            var addColumnCommand = new SqliteCommand(@"
                ALTER TABLE PrivateMessages ADD COLUMN WasReplayed INTEGER NOT NULL DEFAULT 0
            ", connection, transaction);
            addColumnCommand.ExecuteNonQuery();

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

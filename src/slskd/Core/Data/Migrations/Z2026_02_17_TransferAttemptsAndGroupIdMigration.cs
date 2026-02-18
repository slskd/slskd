// <copyright file="Z2026_02_17_TransferAttemptsAndGroupIdMigration.cs" company="slskd Team">
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
using slskd.Transfers;

/// <summary>
///     Updates the Transfers table to add Attempts and GroupId columns.
/// </summary>
public class Z2026_02_17_TransferAttemptsAndGroupIdMigration : IMigration
{
    public Z2026_02_17_TransferAttemptsAndGroupIdMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Transfers];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z2026_02_17_TransferAttemptsAndGroupIdMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        var schema = SchemaInspector.GetDatabaseSchema(ConnectionString);
        var idxes = SchemaInspector.GetDatabaseIndexes(ConnectionString);

        var columns = schema["Transfers"];
        var indexes = idxes["Transfers"];

        // check to see if the Attempts and GroupId columns exist
        if (columns.Any(c => c.Name == nameof(Transfer.Attempts))
            && columns.Any(c => c.Name == nameof(Transfer.GroupId))
            && indexes.Any(i => i.Name.Equals("IDX_Transfers_GroupId", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z2026_02_17_TransferAttemptsAndGroupIdMigration));
            return;
        }

        var columns = SchemaInspector.GetDatabaseSchema(ConnectionString)["Transfers"];

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            void Exec(string sql)
            {
                using var command = new SqliteCommand(sql, connection, transaction);
                command.ExecuteNonQuery();
            }

            Log.Information("> Adding GroupId and Attempts columns to the Transfers table...");

            if (!columns.Any(c => c.Name == nameof(Transfer.GroupId)))
            {
                Exec("ALTER TABLE Transfers ADD COLUMN GroupId TEXT NULL;");
            }

            if (!columns.Any(c => c.Name == nameof(Transfer.Attempts)))
            {
                Exec("ALTER TABLE Transfers ADD COLUMN Attempts INTEGER NOT NULL DEFAULT 0;");
            }

            Log.Information("> New columns added");

            Log.Information("> Adding missing index(es) on the Transfers table...");

            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_GroupId ON Transfers (GroupId)");

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

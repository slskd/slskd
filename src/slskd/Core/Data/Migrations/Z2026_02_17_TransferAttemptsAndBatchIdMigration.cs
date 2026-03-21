// <copyright file="Z2026_02_17_TransferAttemptsAndBatchIdMigration.cs" company="slskd Team">
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
///     Updates the Transfers table to add Attempts and BatchId columns.
/// </summary>
public class Z2026_02_17_TransferAttemptsAndBatchIdMigration : IMigration
{
    public Z2026_02_17_TransferAttemptsAndBatchIdMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Transfers];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z2026_02_17_TransferAttemptsAndBatchIdMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        var schema = SchemaInspector.GetDatabaseSchema(ConnectionString);
        var idxes = SchemaInspector.GetDatabaseIndexes(ConnectionString);

        var columns = schema["Transfers"];
        var indexes = idxes["Transfers"];

        // check to see if the Attempts and BatchId columns exist
        if (columns.Any(c => c.Name == nameof(Transfer.Attempts))
            && columns.Any(c => c.Name == nameof(Transfer.NextAttemptAt))
            && columns.Any(c => c.Name == nameof(Transfer.BatchId))
            && indexes.Any(i => i.Name.Equals("IDX_Transfers_BatchId", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z2026_02_17_TransferAttemptsAndBatchIdMigration));
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

            Log.Information("> Adding BatchId, Attempts, and NextAttemptAt columns to the Transfers table...");

            if (!columns.Any(c => c.Name == nameof(Transfer.BatchId)))
            {
                Exec("ALTER TABLE Transfers ADD COLUMN BatchId TEXT NULL;");
            }

            if (!columns.Any(c => c.Name == nameof(Transfer.Attempts)))
            {
                Exec("ALTER TABLE Transfers ADD COLUMN Attempts INTEGER NOT NULL DEFAULT 0;");
            }

            if (!columns.Any(c => c.Name == nameof(Transfer.NextAttemptAt)))
            {
                Exec("ALTER TABLE Transfers ADD COLUMN NextAttemptAt TEXT NULL;");
            }

            Log.Information("> New columns added");

            Log.Information("> Adding missing index(es) on the Transfers table...");

            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_BatchId ON Transfers (BatchId)");

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

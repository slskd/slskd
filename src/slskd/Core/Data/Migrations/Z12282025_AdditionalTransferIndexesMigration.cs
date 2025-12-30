// <copyright file="Z12282025_AdditionalTransferIndexesMigration.cs" company="slskd Team">
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

/// <summary>
///     Updates the Transfers table to add indexes on the Direction and State columns.
/// </summary>
public class Z12282025_AdditionalTransferIndexesMigration : IMigration
{
    public Z12282025_AdditionalTransferIndexesMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Transfers];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z12282025_AdditionalTransferIndexesMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        // check to see if *BOTH* of the indexes are in place. if one or both are missing, we must apply
        var idxes = SchemaInspector.GetDatabaseIndexes(ConnectionString);
        var txfers = idxes["Transfers"];

        var usernameExists = txfers.Any(c => c.Name.Equals("IDX_Transfers_Username", StringComparison.OrdinalIgnoreCase));
        var removedExists = txfers.Any(c => c.Name.Equals("IDX_Transfers_Removed", StringComparison.OrdinalIgnoreCase));
        var usernameFilenameExists = txfers.Any(c => c.Name.Equals("IDX_Transfers_UsernameFilename", StringComparison.OrdinalIgnoreCase));
        var statsExist = txfers.Any(c => c.Name.Equals("IDX_Transfers_UserUploadStatistics", StringComparison.OrdinalIgnoreCase));

        if (usernameExists && removedExists && usernameFilenameExists && statsExist)
        {
            return false;
        }

        return true;
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z12282025_AdditionalTransferIndexesMigration));
            return;
        }

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

            Log.Information("> Adding missing index(es) on the Transfers table...");

            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_Username ON Transfers (Username)");
            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_Removed ON Transfers (Removed)");
            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_UsernameFilename ON Transfers (Username, Filename)");
            Exec("CREATE INDEX IF NOT EXISTS IDX_Transfers_UserUploadStatistics ON Transfers (Username, Direction, EndedAt, StartedAt, State, Size)");

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

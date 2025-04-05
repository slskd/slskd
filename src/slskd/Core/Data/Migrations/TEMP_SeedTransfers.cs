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
using System.IO;
using Microsoft.Data.Sqlite;
using Serilog;

public class TEMP_SeedTransfers : IMigration
{
    private ILogger Log { get; } = Serilog.Log.ForContext<TransferStateMigration_04012025>();

    public void Apply()
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Program.DataDirectory, "transfers.db")}");
        connection.Open();

        Log.Warning("Staring seed");

        for (long i = 0; i < 10_000; i++)
        {
            string s(Guid id) => @$"
                INSERT INTO Transfers (Id, Username, Direction, Filename, Size, StartOffset, State, RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, AverageSpeed, PlaceInQueue, Removed)
                VALUES ('{id}', 'Username', 'Download', 'Foo.mp3', 42, 0, 'Completed, Succeeded', '1/1/1970 0:0:0', '1/1/1970 0:0:0', '1/1/1970 0:0:0', '1/1/1970 0:0:0', 42, 0, 0, 1);
            ";

            string sql = "";

            for (int j = 0; j < 5_000; j++)
            {
                sql += s(Guid.NewGuid());
            }

            using var cmd = new SqliteCommand(sql, connection);

            cmd.ExecuteNonQuery();
            Log.Warning("Completed {i}", i * 5000);
        }
    }
}

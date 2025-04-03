// <copyright file="Migrator.cs" company="slskd Team">
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

namespace slskd;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using slskd.Migrations;

public interface IMigration
{
    public void Apply();
}

public class Migrator
{
    private string HistoryFile { get; } = Path.Combine(Program.DataDirectory, "misc", "migration.history");
    private ILogger Log { get; } = Serilog.Log.ForContext<Migrator>();

    private Dictionary<string, IMigration> Migrations { get; } = new()
    {
        { nameof(TransferStateMigration_04012025), new TransferStateMigration_04012025() },
    };

    public void Migrate()
    {
        Dictionary<string, DateTime> history = new();

        try
        {
            if (File.Exists(HistoryFile))
            {
                try
                {
                    var txt = File.ReadAllText(HistoryFile);
                    history = txt.FromJson<Dictionary<string, DateTime>>();

                    Log.Debug("Loaded migration history from {HistoryFile}: {History}", HistoryFile, history);
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to load migration history from {HistoryFile}: {Message}", HistoryFile, ex.Message);
                    Log.Warning("Migration history will be overwritten and all migrations will be applied");
                }
            }

            var migrationsNotYetApplied = Migrations.Keys.Except(history.Keys);

            if (!migrationsNotYetApplied.Any())
            {
                Log.Debug("No migrations need to be applied");
            }

            foreach (var migration in Migrations)
            {
                if (history.TryGetValue(migration.Key, out var timestamp))
                {
                    Log.Debug("Migration {Name} was already applied {Date}", migration.Key, timestamp);
                    continue;
                }

                try
                {
                    migration.Value.Apply();
                    history[migration.Key] = DateTime.UtcNow;

                    Log.Information("Migration {Name} was applied successfully", migration.Key);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to apply migration {Name}: {Message}", migration.Key, ex.Message);
                    throw;
                }
            }

            File.WriteAllText(HistoryFile, history.ToJson());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply migrations: {Message}", ex.Message);
            throw;
        }
    }
}
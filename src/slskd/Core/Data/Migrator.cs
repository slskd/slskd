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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Serilog;
using slskd.Migrations;

public interface IMigration
{
    void Apply();
}

/// <summary>
///     Applies database migrations.
/// </summary>
public class Migrator
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Migrator"/> class.
    /// </summary>
    /// <param name="databases">A list of known databases.</param>
    public Migrator(params string[] databases)
    {
        Databases = databases;
    }

    private string HistoryFileName { get; } = Path.Combine(Program.DataDirectory, "migration.history");
    private IEnumerable<string> Databases { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<Migrator>();

    /// <summary>
    ///     A map of all migrations, to be applied in the order they are specified (descending).
    /// </summary>
    private Dictionary<string, IMigration> Migrations { get; } = new()
    {
        // { nameof(TransferStateMigration_04012025), new TransferStateMigration_04012025() },
        { nameof(TEMP_SeedTransfers), new TEMP_SeedTransfers() },
    };

    /// <summary>
    ///     Applies database migrations.
    /// </summary>
    /// <param name="force">Apply all migrations, regardless of whether there's evidence they have been applied already.</param>
    public void Migrate(bool force = false)
    {
        Dictionary<string, DateTime> history = [];

        Log.Information("Checking for outstanding database migrations...");

        // load migration history from the history file in the root of the data directory. this file contains a key/value
        // pair for each migration that's been applied, along with the migration date.
        // if force=true, we don't care about the history and there's no reason to look at it.
        if (!force)
        {
            try
            {
                if (File.Exists(HistoryFileName))
                {
                    var txt = File.ReadAllText(HistoryFileName);
                    history = txt.FromJson<Dictionary<string, DateTime>>();

                    Log.Debug("Loaded migration history from {HistoryFile}: {History}", HistoryFileName, history);
                }
                else
                {
                    Log.Debug("Migration history file {HistoryFile} was not found", HistoryFileName);
                }
            }
            catch (Exception ex)
            {
                // log a message but don't throw; migrations are (should be!) idempotent, so there's no harm in running them,
                // it just takes unnecessary time.
                Log.Warning("Failed to load migration history from {HistoryFile}: {Message}", HistoryFileName, ex.Message);
                Log.Warning("Migration history will be overwritten and all migrations will be applied");
            }
        }

        // figure out which migrations need to be applied by taking the set difference of the migration list and the history contents
        var migrationsNotYetApplied = Migrations.Keys.Except(history.Keys);

        if (!migrationsNotYetApplied.Any())
        {
            Log.Information("Databases are up to date");
            return;
        }

        Log.Warning("{Count} outstanding database migration(s) to apply. This operation must be completed before the application can start.", migrationsNotYetApplied.Count());

        var migrationId = DateTime.UtcNow.ToString("MMddyy_hhmmss");

        Log.Warning("-----> The ID for this migration is {MigrationId}. Use it to locate pre-migration database backups. <-----", migrationId);

        // take some simple backups before we do anything. this gives users a way to get back to a working configuration
        // if the migration is interrupted, fails, or if the user wants to revert to the previous version. these files
        // must be left in place after the migration completes, and they'll be overwritten the next time a migration is applied
        try
        {
            Log.Information("Backing up existing databases...");

            foreach (var database in Databases)
            {
                var name = Path.Combine(Program.DataDirectory, database);
                var src = $"{name}.db";
                var dest = $"{name}.pre-migration.{migrationId}.db";

                File.Copy(src, dest, overwrite: true);

                Log.Information("Backed database {Original} up to {Backup}", src, dest);
            }

            Log.Information("Databases backed up successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to back up one or more database(s) prior to migration, operation cannot continue: {Message}", ex.Message);
            Log.Fatal("Try again, revert the application to the previous version, or delete existing databases (warning: history will be lost!) to continue");
            throw;
        }

        // we know which migrations to apply, and we've backed all of the databases up. let's go!
        try
        {
            Log.Warning("Beginning migration(s)");
            Log.Warning("This may take some time. Avoid stopping the application before the process is complete. If the process is interrupted it may be necessary to manually revert to backups.");

            var overallSw = new Stopwatch();
            overallSw.Start();

            var current = 0;
            var total = migrationsNotYetApplied.Count();

            var currentSw = new Stopwatch();

            foreach (var migration in migrationsNotYetApplied)
            {
                current++;
                currentSw.Restart();

                Log.Warning("Applying migration {Name} ({Current} of {Total})", migration, current, total);

                try
                {
                    Migrations[migration].Apply();
                    history[migration] = DateTime.UtcNow;

                    Log.Information("Migration {Name} was applied successfully (elapsed: {Duration}ms)", migration, currentSw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to apply migration {Name}: {Message}", migration, ex.Message);
                    throw;
                }
            }

            Log.Information("{Count} migration(s) applied successfully (elapsed: {Duration}ms elapsed)", total, overallSw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply one or more migration(s): {Message}", ex.Message);

            try
            {
                Log.Information("Attempting to restore databases from backups...");

                foreach (var database in Databases)
                {
                    var name = Path.Combine(Program.DataDirectory, database);
                    var src = $"{name}.db.pre-migration";
                    var dest = $"{name}.db";

                    // note: leave the backup in place to give users peace of mind
                    File.Copy(src, dest, overwrite: true);

                    Log.Information("Restored {Original} from {Backup}", dest, src);
                }

                Log.Information("Databases restored successfully");
            }
            catch (Exception restoreEx)
            {
                Log.Fatal(restoreEx, "Failed to restore one or more databases from backup: {Message}", restoreEx.Message);
                Log.Fatal($"Restore manually by renaming '<database>.pre-migration.{migrationId}.db' to '<database>.db' prior to starting the application again.");
            }

            throw new SlskdException("Failed to apply one or more database migrations. See inner Exception for details.", ex);
        }

        var newHistory = history.ToJson();
        File.WriteAllText(HistoryFileName, newHistory); // overwrites existing, if present
        Log.Debug("Saved history to {Location}: {History}", HistoryFileName, newHistory);

        Log.Information("Migration(s) complete");
    }

    public static Dictionary<string, IEnumerable<ColumnInfo>> GetDatabaseSchema(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var dict = new Dictionary<string, IEnumerable<ColumnInfo>>();

            using var tableCommand = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection);
            using var tableReader = tableCommand.ExecuteReader();

            while (tableReader.Read())
            {
                var table = tableReader.GetString(0);

                var columns = new List<ColumnInfo>();

                using var columnCommand = new SqliteCommand($"PRAGMA table_info({table});", connection);
                using var cr = columnCommand.ExecuteReader();

                while (cr.Read())
                {
                    columns.Add(new ColumnInfo(
                        Cid: cr.GetInt64(cr.GetOrdinal("cid")),
                        Name: cr.GetString(cr.GetOrdinal("name")),
                        Type: cr.GetString(cr.GetOrdinal("type")),
                        NotNull: cr.GetInt64(cr.GetOrdinal("notnull")) > 0,
                        DefaultValue: cr["dflt_value"],
                        PrimaryKey: cr.GetInt64(cr.GetOrdinal("pk")) > 0));
                }

                dict[table] = columns;
            }

            return dict;
        }
        catch (Exception ex)
        {
            throw new SlskdException($"Failed to retrieve schema information for database '{connectionString}'. The database might be corrupt or in use by another application; if the problem persists the backing file may need to be deleted", ex);
        }
    }

    public record ColumnInfo(long Cid, string Name, string Type, bool NotNull, object DefaultValue, bool PrimaryKey);
}
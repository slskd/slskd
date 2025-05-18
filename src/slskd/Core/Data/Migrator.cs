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
using Serilog;
using slskd.Migrations;

/// <summary>
///     Applies database migrations.
/// </summary>
public interface IMigration
{
    /// <summary>
    ///     Determines whether the migration needs to be applied.
    /// </summary>
    /// <remarks>
    ///     This method MUST be read-only and should contain no side effects.
    /// </remarks>
    /// <returns>A value indicating whether the migration needs to be applied.</returns>
    bool NeedsToBeApplied();

    /// <summary>
    ///    Applies the migration.
    /// </summary>
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

    private IEnumerable<string> Databases { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<Migrator>();

    /// <summary>
    ///     A map of all migrations, to be applied in the order they are specified (descending).
    /// </summary>
    private Dictionary<string, IMigration> Migrations { get; } = new()
    {
        { nameof(Z04012025_TransferStateMigration), new Z04012025_TransferStateMigration() },
    };

    /// <summary>
    ///     Applies database migrations.
    /// </summary>
    /// <param name="force">Apply all migrations, regardless of whether there's evidence they have been applied already.</param>
    public void Migrate(bool force = false)
    {
        List<string> migrationsToApply = [];
        List<string> migrationsToSkip = [];

        /*
            determine which migrations need to be applied

            for the time being, we'll rely on each of the migrations to determine whether they need to be applied
            this will work until:
              1) there's a migration that isn't idempotent (can't tell whether it needs to be applied on its own), or..
              2) there are > ~20 migrations, making repeated I/O of database files too slow/expensive on low spec hardware

            when the time comes, migration history should be stored within the database files themselves.
        */
        try
        {
            foreach (var migration in Migrations.Keys)
            {
                // warning: performs I/O
                if (!Migrations[migration].NeedsToBeApplied())
                {
                    Log.Debug("Migration {Name} does not need to be applied", migration);
                    migrationsToSkip.Add(migration);
                }
                else
                {
                    Log.Debug("Migration {Name} needs to be applied", migration);
                    migrationsToApply.Add(migration);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to disposition migrations: {Message}", ex.Message);
            throw;
        }

        Log.Debug("Migrations: {Migrations}", new { migrationsToApply, migrationsToSkip });

        if (migrationsToApply.Count == 0)
        {
            Log.Information("Databases are up to date!");
            return;
        }

        /*
            MIGRATION TIME!
            ---------------------------------------------------------------------------------------------------------------------------------------------------
        */
        Log.Warning("{Count} outstanding database migration(s) to apply. This operation must be completed before the application can start.", migrationsToApply.Count());

        var migrationId = DateTime.UtcNow.ToString("MMddyy_hhmmss");

        Log.Warning("--> The ID for this migration is {MigrationId}. Use it to locate pre-migration database backups, should manual cleanup be needed. <--", migrationId);

        // take some simple backups before we do anything. this gives users a way to get back to a working configuration
        // if the migration is interrupted, fails, or if the user wants to revert to the previous version. these files
        // must be left in place after the migration completes, and they'll be overwritten the next time a migration is applied
        try
        {
            Log.Information("Backing up existing databases...");

            foreach (var database in Databases)
            {
                var src = MakeSourceDatabasePath(database);
                var dest = MakeBackupDatabasePath(database, migrationId);

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
            var total = migrationsToApply.Count();

            var currentSw = new Stopwatch();

            foreach (var migration in migrationsToApply)
            {
                current++;
                currentSw.Restart();

                Log.Warning("Applying migration {Name} ({Current} of {Total})", migration, current, total);

                try
                {
                    Migrations[migration].Apply();

                    Log.Information("Migration {Name} was applied successfully (elapsed: {Duration}ms)", migration, currentSw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to apply migration {Name}: {Message}", migration, ex.Message);
                    throw;
                }
            }

            Log.Information("{Count} migration(s) applied successfully (elapsed: {Duration}ms)", total, overallSw.ElapsedMilliseconds);
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
                    var src = MakeBackupDatabasePath(database, migrationId);
                    var dest = MakeSourceDatabasePath(database);

                    // note: leave the backup in place just in case; automatic cleanup will delete them later
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

        Log.Information("Migration(s) complete!");
    }

    private string MakeSourceDatabasePath(string database) => Path.Combine(Program.DataDirectory, $"{database}.db");
    private string MakeBackupDatabasePath(string database, string timestamp) => Path.Combine(Program.DataBackupDirectory, $"{database}.pre-migration-backup.{timestamp}.db");
}
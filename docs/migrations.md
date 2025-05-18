# Database Migrations

Database migrations are used to update the structure of the application's databases to support new features or improve functionality. These migrations are applied automatically when the application starts. If a migration fails, the application will attempt to restore the database to its pre-migration state using backups created before the migration process began.

## Migration Process

When the application detects that a migration is required, it performs the following steps:

1. Back up existing databases: Before applying any changes, the application creates a backup of each database. These backups are stored in the `migrations` directory within the application's data directory. The backup files are named using the format `<database>.pre-migration-backup.<migration id>.db`.

2. Apply migrations: The application applies the necessary changes to the database schema and data.

At the start of each migration a unique `migration id` is generated. If anything goes wrong, this ID can be used to locate the backup files created before the error occured (as subsequent migration attempts/retries will generate new backups, which may contain partially applied migrations).

## What to Do If a Migration Fails

If a migration fails, the application will attempt to restore the databases from the backups created before the migration. However, if the automatic restoration process fails, you can manually recover your data by following these steps:

1. Locate the Backup Files:
   - Find the _first_ failed migration attempt and note the associated ID (it will be in application logs). If it can't be located, it is safe to assume that the oldest ID is the correct one.
   - Navigate to the `migrations` directory within the application's data directory.
   - Look for files named `<database>.pre-migration-backup.<migration id>.db`.

2. Restore the Backup:
   - Rename the backup file to replace the current database file. For example, rename `transfers.pre-migration-backup.040123_123456.db` to `transfers.db`.
   - Ensure the restored file is placed in the root of the application's data directory (move it up, out of the `migrations` directory).

3. Restart the Application:
   - Start the application again. It will detect the restored database and attempt to reapply the migration.

## If the Migration Continues to Fail

If the application gets "stuck" in a reboot loop and can't complete migration(s) successfully, you will need to rename (or delete) the existing database files and allow the application to re-generate them.

It should be possible to recover the old data, but the process will need to be manual.  Create a GitHub issue and provide logs from the failing migration(s).

## Preventing Migration Issues

To minimize the risk of migration failures:

- Ensure Sufficient Disk Space: Verify that there is enough disk space available for creating backups and applying migrations.
- Avoid Interruptions: Do not stop the application while a migration is in progress.
- Monitor Logs: Check the application logs for detailed information about the migration process and any errors encountered.


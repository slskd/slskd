# Database Migrations

In the hopefully unlikely event that a migration is necessary, a migration must be written.

Migrations are applied by the `Migrator` at startup.

The `history` file in the data directory is used to attempt to track whether migrations have already been applied.  If a migration appears in the history file it won't be run again.  Users can delete or modify the `history` file or use the `force` flag to apply them, meaning migrations **MUST** be idempotent.

The `Migrator` creates a backup of each database prior to running any migrations, and if an exception is thrown at any time during the process the `Migrator` will revert to these backups automatically.  The backup files are left in place when the migration is complete so that users can manually revert if the migration left their application in a bad state or destroyed or lost data.

## Creating a Migration

Each migration must be created as a class that implements the `IMigration` interface.

Migrations MUST:

* Figure out where on disk the associated database(s) are located. This is accomplished by combining the static `Program.DataDirectory` variable
and the name(s) of the table(s) that need to be migrated.
* Log progress, so users can see that the application is performing work while records are updated.
* Use transactions when performing database operations and using/try catch where appropriate to avoid partially applied migrations.
* Inspect the target database(s) schema to determine whether the migration needs to be applied (if possible)

When the migration is complete, edit the `Migrations` property in the `Migrator` to add the migration to the list in the proper order.

### Above all else, each and every migration **MUST** be idempotent.

Users can delete the history file or use the `force` flag to cause migrations to be run again.  We can inspect schemas to determine whether re-running is necessary, but there may be cases where that's not enough.
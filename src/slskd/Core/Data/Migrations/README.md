# Database Migrations

In the hopefully unlikely event that a non-backwards-compatible schema change to a database migration is necessary, a migration must be written.

Migrations are applied by the `Migrator` at startup.

The `history` file in the data directory is used to attempt to track whether migrations have already been applied.  If a migration appears in the history file it won't be run again.  Users can delete or modify the `history` file or use the `force` flag to apply them, meaning migrations **MUST** be idempotent.

The `Migrator` creates a backup of each database prior to running any migrations, and if an exception is thrown at any time during the process the `Migrator` will revert to these backups automatically.  The backup files are left in place when the migration is complete so that users can manually revert if the migration left their application in a bad state or destroyed or lost data.

## Creating a Migration

Each migration must be created as a class that implements the `IMigration` interface.  The naming convention for migration classes is:

```
z<MMDDYYYY>_<ShortDescription>Migration
```

Migrations MUST:

* Figure out where on disk the associated database(s) are located. This is accomplished by combining the static `Program.DataDirectory` variable
and the name(s) of the table(s) that need to be migrated.
* Inspect the target database(s) schema to determine whether the migration needs to be applied (if possible)
* Use transactions when performing database operations and using/try catch where appropriate to avoid partially applied migrations.
* Log progress, so users can see that the application is performing work while records are updated.

The new `IMigration` implementation must be added to the dictionary in the `Migrations` property of the `Migrator` in the desired order so that it can be run.

### Above all else, each and every migration **MUST** be idempotent.

Users can delete the history file or use the `force` flag to cause migrations to be run again.  We can inspect schemas to determine whether re-running is necessary, but there may be cases where that's not enough.
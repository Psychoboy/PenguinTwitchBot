# Database Migration & Debug Helpers

This folder contains batch scripts to simplify database management across both database providers (PostgreSQL, SQLite).

## Quick Migration Scripts

### `add-migration.bat MigrationName`
Generates a new migration for both database providers.

**Usage:**
```batch
add-migration.bat AddUserFeature
add-migration.bat CreateProductTable
```

This automatically generates migrations in:
- `PenguinTwitchBot.Migrations.Postgres/Migrations/`
- `PenguinTwitchBot.Migrations.Sqlite/Migrations/`

### `remove-migration.bat`
Removes the last migration from both database providers.

**Usage:**
```batch
remove-migration.bat
```

WARNING: Use with caution - this removes pending migrations from all providers.

### `update-database.bat`
Applies all pending migrations to both database providers.

**Usage:**
```batch
update-database.bat
```

This will update the database schemas in PostgreSQL and SQLite.

## Advanced: `manage-migrations.bat`

Interactive menu for comprehensive migration management.

**Features:**
- Generate new migration
- Remove last migration
- Update database
- Show migration status
- Exit

**Usage:**
```batch
manage-migrations.bat
```

Navigate using numbered options (1-5).

## Debug Startup: Database Provider Selection

### Launch Profiles in VS Code
When running in debug mode with F5, you can now select which database provider to use:

1. **PenguinTwitchBot** - Uses config file provider (default: SQLite)
2. **PenguinTwitchBot (PostgreSQL)** - Forces PostgreSQL
3. **PenguinTwitchBot (SQLite)** - Forces SQLite

These profiles are configured in `PenguinTwitchBot/Properties/launchSettings.json`.

### Command Line Startup
```batch
run-with-db-selection.bat
```
Interactive menu to select database provider and start the application with `dotnet run`.

### Environment Variable Override
You can also set the `DATABASE_PROVIDER` environment variable directly:

```batch
set DATABASE_PROVIDER=postgres
dotnet run --project PenguinTwitchBot
```

Valid values: `postgres`, `postgresql`, `sqlite`

## Workflow Examples

### Adding a new feature with migrations for all providers:
```batch
add-migration.bat AddFishingLeaderboard
REM Review the generated migration files
update-database.bat
```

### Quick database provider switching during development:
```batch
REM Start with PostgreSQL
run-with-db-selection.bat
```
Then select option "2" for PostgreSQL.

### Testing migration compatibility across providers:
```batch
add-migration.bat TestFeature
update-database.bat
REM Verify all 2 databases updated successfully
remove-migration.bat
```

## Configuration

The primary database provider is defined in `PenguinTwitchBot/appsettings.secrets.json`:
```json
{
  "Database": {
    "Provider": "sqlite"
  },
  "ConnectionStrings": {
    "PostgresConnection": "Host=...;",
    "SqliteConnection": "Data Source=..."
  }
}
```

Debug profiles and environment variables override this during development.

## Notes

- Migrations must be compatible across both database systems
- Keep migrations database-agnostic; use EF Core's provider-agnostic patterns
- Test migrations on all providers before deployment
- Each migration project has its own `Migrations/` folder with provider-specific SQL generation

## Provider-Specific Migration Differences

Migration names should match across providers, but generated migration bodies can differ.
This is expected because provider SQL types and SQL generation differ (for example,
PostgreSQL uses timezone-aware timestamp handling while SQLite does not).

## UTC and Legacy Data

The application now writes UTC timestamps for behavior-critical data paths.

- Existing historical rows written in local time are not automatically converted by startup code.
- If you restore legacy data, normalize timestamps during restore/import before relying on UTC-based expiry, cooldown, or scheduling logic.
- Backup file names and backup file retention checks intentionally remain local-time based because they are file-system/operator workflows, not runtime behavior data.

## Username/Key Normalization and Legacy Rows

New writes normalize lookup-key style values (for example usernames and game setting keys).
If your restored data contains legacy mixed-case key rows, normalize those rows during restore/import to avoid duplicates and inconsistent matching semantics across providers.

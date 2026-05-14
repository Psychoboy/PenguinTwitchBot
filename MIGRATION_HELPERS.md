# Database Migration & Debug Helpers

This folder contains batch scripts to simplify database management across all three database providers (MariaDB, PostgreSQL, SQLite).

## Quick Migration Scripts

### `add-migration.bat MigrationName`
Generates a new migration for all 3 database providers.

**Usage:**
```batch
add-migration.bat AddUserFeature
add-migration.bat CreateProductTable
```

This automatically generates migrations in:
- `DotNetTwitchBot.Migrations.MariaDb/Migrations/`
- `DotNetTwitchBot.Migrations.Postgres/Migrations/`
- `DotNetTwitchBot.Migrations.Sqlite/Migrations/`

### `remove-migration.bat`
Removes the last migration from all 3 database providers.

**Usage:**
```batch
remove-migration.bat
```

⚠️ Use with caution - this removes pending migrations from all providers.

### `update-database.bat`
Applies all pending migrations to all 3 database providers.

**Usage:**
```batch
update-database.bat
```

This will update the database schemas in MariaDB, PostgreSQL, and SQLite.

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

1. **DotNetTwitchBot** - Uses config file provider (default: MariaDB)
2. **DotNetTwitchBot (MariaDB)** - Forces MariaDB
3. **DotNetTwitchBot (PostgreSQL)** - Forces PostgreSQL
4. **DotNetTwitchBot (SQLite)** - Forces SQLite

These profiles are configured in `DotNetTwitchBot/Properties/launchSettings.json`.

### Command Line Startup
```batch
run-with-db-selection.bat
```
Interactive menu to select database provider and start the application with `dotnet run`.

### Environment Variable Override
You can also set the `DATABASE_PROVIDER` environment variable directly:

```batch
set DATABASE_PROVIDER=postgres
dotnet run --project DotNetTwitchBot
```

Valid values: `mariadb`, `mysql`, `postgres`, `postgresql`, `sqlite`

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
REM Verify all 3 databases updated successfully
remove-migration.bat
```

## Configuration

The primary database provider is defined in `DotNetTwitchBot/appsettings.secrets.json`:
```json
{
  "Database": {
    "Provider": "mariadb"
  },
  "ConnectionStrings": {
    "MariaDbConnection": "Server=...;",
    "PostgresConnection": "Host=...;",
    "SqliteConnection": "Data Source=..."
  }
}
```

Debug profiles and environment variables override this during development.

## Notes

- Migrations must be compatible across all 3 database systems
- Keep migrations database-agnostic; use EF Core's provider-agnostic patterns
- Test migrations on all providers before deployment
- Each migration project has its own `Migrations/` folder with provider-specific SQL generation

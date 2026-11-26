using Serilog;
using System.Reflection;

internal partial class Program
{
    private static string GetDatabaseProvider(IConfiguration configuration)
    {
        // Check environment variable first (for debug profile selection)
        var envProvider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(envProvider))
        {
            Log.Information("Using database provider {Provider} from environment variable", envProvider);
            return envProvider switch
            {
                "postgres" or "postgresql" => "postgres",
                "sqlite" => "sqlite",
                _ => throw new InvalidOperationException($"Unsupported database provider '{envProvider}'. Supported values: postgres, postgresql, sqlite.")
            };
        }

        // Fall back to configuration file
        var provider = configuration.GetValue<string>("Database:Provider")?.Trim().ToLowerInvariant();
        return provider switch
        {
            null or "" => "sqlite",
            "postgres" => "postgres",
            "postgresql" => "postgres",
            "sqlite" => "sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: postgres, postgresql, sqlite.")
        };
    }

    private static void PreloadMigrationAssemblies()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] migrationDlls =
        [
            "PenguinTwitchBot.Migrations.Postgres.dll",
            "PenguinTwitchBot.Migrations.Sqlite.dll",
        ];
        foreach (var dll in migrationDlls)
        {
            var path = Path.Combine(baseDir, dll);
            if (File.Exists(path))
                System.Reflection.Assembly.LoadFrom(path);
        }
    }

    private static string GetMigrationsAssembly(string provider)
    {
        return provider switch
        {
            "postgres" => "PenguinTwitchBot.Migrations.Postgres",
            "sqlite" => "PenguinTwitchBot.Migrations.Sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };
    }

    private static string GetConnectionString(IConfiguration configuration, string provider)
    {
        var configured = provider switch
        {
            "postgres" => configuration.GetConnectionString("PostgresConnection"),
            "sqlite" => string.IsNullOrEmpty(configuration.GetConnectionString("SqliteConnection"))
                ? $"Data Source={Path.Combine(AppContext.BaseDirectory, "Data", "PenguinTwitchBot.sqlite")}"
                : configuration.GetConnectionString("SqliteConnection"),
            _ => null
        };

        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException($"Connection string is missing for provider '{provider}'.")
            : configured;
    }

    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options, string provider, string connectionString, string migrationsAssembly)
    {
        // Downgrade pending-model-changes from error to warning so the app can start
        // while a migration is being prepared.
        options.ConfigureWarnings(w =>
        {
            w.Log(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);

            // SubActionType uses table-per-concrete-type. This warning targets database-generated key defaults
            // (e.g. identity columns) that could collide across tables; our Guid keys are generated client-side
            // by UuidV7ValueGenerator, so it does not apply.
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.TpcStoreGeneratedIdentityWarning);

            // SQLite emits this warning when it needs PRAGMA foreign_keys toggles around
            // schema updates. This is expected for provider-generated migration SQL.
            if (provider == "sqlite")
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.NonTransactionalMigrationOperationWarning);
            }
        });

        switch (provider)
        {
            case "postgres":
                // Sub-action queries over the table-per-type hierarchy are ~300 KB of SQL each. Auto-preparing
                // them avoids re-sending and re-planning that text on every call. Set "Max Auto Prepare=0"
                // in the connection string to opt out (e.g. behind a transaction-mode pooler).
                var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                if (!npgsqlBuilder.ContainsKey("Max Auto Prepare"))
                {
                    npgsqlBuilder.MaxAutoPrepare = 20;
                    connectionString = npgsqlBuilder.ConnectionString;
                }
                options.UseNpgsql(connectionString, postgresOptions =>
                {
                    postgresOptions.MigrationsAssembly(migrationsAssembly)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(15),
                            errorCodesToAdd: null
                        );
                });
                break;

            case "sqlite":
                // WAL + foreign keys are enabled via SqliteWalInterceptor. Shared-cache mode
                // (Cache=Shared) is intentionally avoided: it causes cross-connection table
                // locks ("database table is locked", SQLITE_LOCKED) under concurrent reads/writes.
                var sqliteConnStr = connectionString!.Contains("Cache=", StringComparison.OrdinalIgnoreCase)
                    ? connectionString
                    : connectionString + ";Foreign Keys=True";
                options.UseSqlite(sqliteConnStr, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(migrationsAssembly)
                        .CommandTimeout(30);
                });
                options.AddInterceptors(new SqliteWalInterceptor());
                break;

            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }
    }
}

/// <summary>
/// Applies WAL journal mode and busy timeout PRAGMAs to every SQLite connection opened by EF Core.
/// Required for multi-threaded ASP.NET Core workloads to avoid "database is locked" errors.
/// </summary>
file sealed class SqliteWalInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Repository;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public class BackupTools : IBackupTools
    {
        private readonly IFileSystem _fs;
        private readonly ILogger _logger;
        private readonly IZipService _zipService;

        public string BackupDirectory { get; }

        public BackupTools(IFileSystem fileSystem, ILogger<BackupTools> logger, IZipService zipService)
        {
            _fs = fileSystem;
            _logger = logger;
            _zipService = zipService;
            BackupDirectory = _fs.Path.Combine(_fs.Directory.GetCurrentDirectory(), "Data", "backups");
        }

        public List<FileInfo> GetBackupFiles(string backupDirectory)
        {
            return _fs.Directory.GetFiles(backupDirectory, "*.zip").Select(x => new FileInfo(x)).ToList();
        }

        public async Task BackupTable<T>(DbContext context, string backupDirectory, ILogger? logger) where T : class
        {
            var fileName = _fs.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = true
            };
            await using var fileStream = _fs.File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new System.Text.Json.Utf8JsonWriter(fileStream);
            writer.WriteStartArray();
            var count = 0;
            await foreach (var record in context.Set<T>().AsNoTracking().AsAsyncEnumerable())
            {
                JsonSerializer.Serialize(writer, record, options);
                count++;
                if (count % 500 == 0)
                    await writer.FlushAsync();
            }
            writer.WriteEndArray();
            await writer.FlushAsync();
            logger?.LogDebug("Backed up {Count} records to {Name}", count, typeof(T).Name);
        }

        public async Task WriteData<T>(string backupDirectory, List<T> records, ILogger? logger)
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = true
            };
            var fileName = _fs.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
            await using var fileStream = _fs.File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fileStream, records, options);
            logger?.LogDebug("Backed up {Count} records to {Name}", records.Count, typeof(T).Name);
        }

        public async Task RestoreTable<T>(DbContext context, string backupDirectory, ILogger? logger) where T : class
        {
            try
            {
                var fileName = _fs.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
                if (!_fs.File.Exists(fileName)) return;

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                await using var fileStream = _fs.File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var records = await JsonSerializer.DeserializeAsync<List<T>>(fileStream, options);
                if (records == null) throw new Exception($"{typeof(T).Name}.json was null");

                await context.Set<T>().ExecuteDeleteAsync();
                context.Set<T>().AddRange(records);
                logger?.LogDebug("Restored {Count} records from {Name}", records.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(T).Name);
                throw;
            }
        }

        public async Task BackupDatabase(DbContext context, string backupDirectory, ILogger logger)
        {
            logger.LogInformation("Backing up database");
            var tempDirectory = _fs.Path.Combine(backupDirectory, $"temp-{Guid.NewGuid():N}");
            if (!_fs.Directory.Exists(backupDirectory))
            {
                _fs.Directory.CreateDirectory(backupDirectory);
            }

            _fs.Directory.CreateDirectory(tempDirectory);
            
            var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s =>
            {
                try { return s.GetTypes(); }
                catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
            })
            .Where(p => typeof(IBackupDb).IsAssignableFrom(p) && p.IsClass && p.FullName?.Contains("GenericRepository") == false)
            .Where(p => p.GetConstructors().Any(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == 1 && typeof(DbContext).IsAssignableFrom(parameters[0].ParameterType);
            }));

            foreach (var handler in handlers)
            {
                IBackupDb? handlerInstance = null;
                try
                {
                    var ctor = handler.GetConstructors().First(c =>
                    {
                        var parameters = c.GetParameters();
                        return parameters.Length == 1 && typeof(DbContext).IsAssignableFrom(parameters[0].ParameterType);
                    });
                    handlerInstance = (IBackupDb?)ctor.Invoke(new object[] { context });
                }
                catch (MissingMethodException)
                {
                    logger.LogWarning("Skipping backup handler {Name}: incompatible constructor", handler.Name);
                    continue;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to instantiate backup handler {Name}", handler.Name);
                    continue;
                }
                if (handlerInstance == null)
                {
                    logger.LogError("Failed to create instance of {Name}", handler.Name);
                    continue;
                }
                try
                {
                    await handlerInstance.BackupTable(context, tempDirectory, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to backup {Name}", handler.Name);
                }
            }

            var startPath = tempDirectory;
            var zipPath = _fs.Path.Combine(backupDirectory, string.Format("backup-{0}.zip", DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")));
            _zipService.CreateFromDirectory(startPath, zipPath);
            _fs.Directory.Delete(tempDirectory, true);
            logger?.LogInformation($"Backup created at {zipPath}");

            var orphaned = _fs.Directory.GetDirectories(backupDirectory, "temp-*");
            if (orphaned.Length > 0)
            {
                logger?.LogWarning("Found {Count} orphaned temp directories in {BackupDirectory}. Manually delete them to conserve disk space. Paths: {Paths}",
                    orphaned.Length, backupDirectory, string.Join("; ", orphaned));
                foreach (var leftover in orphaned)
                {
                    logger?.LogWarning("Orphaned temp directory detected: {Path}", leftover);
                }
            }
        }

        public async Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger)
        {
            logger?.LogInformation("Restoring database");

            // Reset fishing deletion flag at the start of restore
            Repository.Repositories.FishingRepository.ResetDeletionFlag();

            // Get all handler types
            var allHandlers = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s =>
                {
                    try { return s.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
                })
                .Where(p => typeof(IBackupDb).IsAssignableFrom(p) && p.IsClass && p.FullName?.Contains("GenericRepository") == false)
                .Where(p => p.GetConstructors().Any(c =>
                {
                    var parameters = c.GetParameters();
                    return parameters.Length == 1 && typeof(DbContext).IsAssignableFrom(parameters[0].ParameterType);
                }))
                .ToList();

            // Define restore order priority (lower number = restore first)
            var restoreOrder = new Dictionary<string, int>
            {
                { "ActionsRepository", 1 },        // First (includes SubActions and Triggers)
                { "SubActionsRepository", 999 },   // Skip (no-op)
                { "TriggersRepository", 999 },     // Skip (no-op)
                { "ActionTriggersRepository", 999 }, // Skip (no-op, deprecated)

                // Fishing system - must restore in dependency order
                { "FishingRepository", 10 },       // FishType first (master table)
                { "FishingShopItemRepository", 11 }, // FishingShopItem second (references FishType)
                { "FishCatchRepository", 12 },     // FishCatch (depends on FishType)
                { "UserFishingBoostRepository", 13 }, // UserFishingBoost (depends on FishingShopItem)
                { "FishingGoldRepository", 14 },   // FishingGold (no dependencies)
                { "FishingSettingsRepository", 15 }, // FishingSettings (no dependencies)
                { "FishingSnapEventRepository", 16 } // FishingSnapEvent (historical, no dependencies)
            };

            // Sort handlers by priority
            var orderedHandlers = allHandlers
                .OrderBy(h => restoreOrder.TryGetValue(h.Name, out int priority) ? priority : 100) // Default priority 100
                .ThenBy(h => h.Name) // Alphabetical for same priority
                .ToList();

            logger?.LogDebug("Restore order: {Handlers}", string.Join(", ", orderedHandlers.Select(h => h.Name)));

            var errors = "";
            var currentPriority = -1;

            foreach (var handler in orderedHandlers)
            {
                try
                {
                    var ctor = handler.GetConstructors().First(c =>
                    {
                        var parameters = c.GetParameters();
                        return parameters.Length == 1 && typeof(DbContext).IsAssignableFrom(parameters[0].ParameterType);
                    });
                    var handlerInstance = (IBackupDb?)ctor.Invoke(new object[] { context });
                    if (handlerInstance == null)
                    {
                        logger?.LogError("Failed to create instance of {Name}", handler.Name);
                        continue;
                    }

                    var priority = restoreOrder.TryGetValue(handler.Name, out int p) ? p : 100;

                    // Save changes when priority level changes (to ensure previous entities are persisted)
                    // Priority 1 (Actions) must be saved before Priority 100 (everything else)
                    if (currentPriority != -1 && currentPriority != priority && priority < 100)
                    {
                        logger?.LogDebug("Committing priority {Priority} entities before moving to priority {NextPriority}", currentPriority, priority);
                        await context.SaveChangesAsync();
                        context.ChangeTracker.Clear(); // Clear after save to avoid conflicts
                    }

                    currentPriority = priority;

                    await handlerInstance.RestoreTable(context, backupDirectory, logger);
                }
                catch (Exception ex)
                {
                    errors += $"{handler.Name}: {ex.Message}\n";
                }
            }

            logger?.LogInformation("Database committing final changes");
            await context.SaveChangesAsync();

            // Post-restore: Remap entity references now that all tables have their new IDs
            logger?.LogInformation("Starting post-restore entity reference remapping");
            var actionsRepository = new Repository.Repositories.ActionsRepository((ApplicationDbContext)context);
            await actionsRepository.RemapEntityReferencesAfterRestore(logger);

            // Post-restore: Reset PostgreSQL sequences that may have de-synced due to explicit ID inserts.
            // SQLite ROWID advance automatically on explicit inserts, so this is PostgreSQL-only.
            if (context.Database.ProviderName?.Contains("Npgsql") == true)
            {
                logger?.LogInformation("Resetting PostgreSQL sequences after restore");
                await ResetPostgresSequencesAsync(context, logger);
            }

            if (!string.IsNullOrEmpty(errors))
            {
                logger?.LogError("Errors occurred during restore:\n{Errors}", errors);
            }
            logger?.LogInformation("Database restored");
        }

        private static async Task ResetPostgresSequencesAsync(DbContext context, ILogger? logger)
        {
            var tables = new[]
            {
                "PointTypes",
            };

            foreach (var table in tables)
            {
                try
                {
#pragma warning disable EF1002
                    await context.Database.ExecuteSqlRawAsync(
                        $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"{table}\"), 1), true)");
#pragma warning restore EF1002
                    logger?.LogDebug("Reset sequence for table {Table}", table);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to reset sequence for table {Table}", table);
                }
            }
        }
    }
}

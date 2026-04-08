using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Repository;
using EFCore.BulkExtensions;
using LinqToDB.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.DatabaseTools
{
    public class BackupTools
    {
        public static string BACKUP_DIRECTORY = Path.Combine(Directory.GetCurrentDirectory(), "Data", "backups");
        public static List<FileInfo> GetBackupFiles(string backupDirectory)
        {
            return Directory.GetFiles(backupDirectory, "*.zip").Select(x => new FileInfo(x)).ToList();
        }

        public static async Task BackupTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class
        {
            var records = await context.Set<T>().ToListAsync();
            await WriteData(backupDirectory, records, logger);
        }

        public static async Task WriteData<T>(string backupDirectory, List<T> records, ILogger? logger = null)
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(records, options);

            var fileName = $"{backupDirectory}/{typeof(T).Name}.json";
            await File.WriteAllTextAsync(fileName, json, encoding: System.Text.Encoding.UTF8);
            logger?.LogDebug("Backed up {Count} records to {Name}", records.Count, typeof(T).Name);
        }

        public static async Task RestoreTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(T).Name}.json";
                if (!File.Exists(fileName)) return;

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<T>>(json, options);
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

        public static async Task BackupDatabase(DbContext context, string backupDirectory, ILogger logger)
        {
            logger.LogInformation("Backing up database");
            var tempDirectory = Path.Combine(backupDirectory, "temp");
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
            var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IBackupDb).IsAssignableFrom(p) && p.IsClass && p.FullName?.Contains("GenericRepository") == false);

            foreach (var handler in handlers)
            {
                var handlerInstance = (IBackupDb?)Activator.CreateInstance(handler, context);
                if (handlerInstance == null)
                {
                    logger.LogError("Failed to create instance of {Name}", handler.Name);
                    continue;
                }
                await handlerInstance.BackupTable(context, tempDirectory, logger);

            }

            var startPath = tempDirectory;
            var zipPath = Path.Combine(backupDirectory, string.Format("backup-{0}.zip", DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")));
            ZipFile.CreateFromDirectory(startPath, zipPath);
            Directory.Delete(tempDirectory, true);
            logger?.LogInformation($"Backup created at {zipPath}");
        }

        public static async Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            logger?.LogInformation("Restoring database");

            // Get all handler types
            var allHandlers = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IBackupDb).IsAssignableFrom(p) && p.IsClass && p.FullName?.Contains("GenericRepository") == false)
                .ToList();

            // Define restore order priority (lower number = restore first)
            var restoreOrder = new Dictionary<string, int>
            {
                { "ActionsRepository", 1 },        // First (includes SubActions and Triggers)
                { "SubActionsRepository", 999 },   // Skip (no-op)
                { "TriggersRepository", 999 },     // Skip (no-op)
                { "ActionTriggersRepository", 999 } // Skip (no-op, deprecated)
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
                    var handlerInstance = (IBackupDb?)Activator.CreateInstance(handler, context);
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

            if (!string.IsNullOrEmpty(errors))
            {
                logger?.LogError("Errors occurred during restore:\n{Errors}", errors);
            }
            logger?.LogInformation("Database restored");
        }
    }
}

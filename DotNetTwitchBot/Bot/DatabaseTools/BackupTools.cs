using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Repository;
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
            var json = JsonSerializer.Serialize(records);

            var fileName = $"{backupDirectory}/{typeof(T).Name}.json";
            await File.WriteAllTextAsync(fileName, json, encoding: System.Text.Encoding.UTF8);
            logger?.LogDebug("Backed up {Count} records to {Name}", records.Count, typeof(T).Name);
        }

        public static async Task RestoreTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class
        {
            var fileName = $"{backupDirectory}/{typeof(T).Name}.json";
            if (!File.Exists(fileName)) return;
            var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);
            var records = JsonSerializer.Deserialize<List<T>>(json);
            if(records == null) throw new Exception($"{typeof(T).Name}.json was null");
            context.Set<T>().RemoveRange(context.Set<T>());
            context.Set<T>().AddRange(records);
            logger?.LogDebug("Restored {Count} records from {Name}", records.Count, typeof(T).Name);
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
            var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IBackupDb).IsAssignableFrom(p) && p.IsClass && p.FullName?.Contains("GenericRepository") == false);

            foreach (var handler in handlers)
            {
                var handlerInstance = (IBackupDb?)Activator.CreateInstance(handler, context);
                if (handlerInstance == null)
                {
                    logger?.LogError("Failed to create instance of {Name}", handler.Name);
                    continue;
                }
                await handlerInstance.RestoreTable(context, backupDirectory, logger);

            }
            logger?.LogInformation("Database committing");
            await context.SaveChangesAsync();
            logger?.LogInformation("Database restored");
        }
    }
}

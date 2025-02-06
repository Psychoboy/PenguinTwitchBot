using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Bot.Models.Timers;
using DotNetTwitchBot.Bot.Models.Wheel;
using System.IO.Compression;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.DatabaseTools
{
    public class BackupTools
    {
        public static string BACKUP_DIRECTORY = Path.Combine(Directory.GetCurrentDirectory(), "Data", "backups");
        public static List<string> GetBackupFiles(string backupDirectory)
        {
            return [.. Directory.GetFiles(backupDirectory, "*.zip").Order()];
        }

        public static async Task BackupTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class
        {
            var records = await context.Set<T>().ToListAsync();
            var json = JsonSerializer.Serialize(records);
            var fileName = $"{backupDirectory}/{typeof(T).Name}.json";
            await File.WriteAllTextAsync(fileName, json, encoding: System.Text.Encoding.UTF8);
            logger?.LogDebug($"Backed up {records.Count} records to {typeof(T).Name}");
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
            logger?.LogDebug($"Restored {records.Count} records from {typeof(T).Name}");
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

            await BackupTable<GiveawayEntry>(context, tempDirectory, logger);
            await BackupTable<GiveawayWinner>(context, tempDirectory, logger);
            await BackupTable<GiveawayExclusion>(context, tempDirectory, logger);
            await BackupTable<Viewer>(context, tempDirectory, logger);
            await BackupTable<ViewerTicket>(context, tempDirectory, logger);
            await BackupTable<Counter>(context, tempDirectory, logger);
            await BackupTable<CustomCommands>(context, tempDirectory, logger);
            await BackupTable<AudioCommand>(context, tempDirectory, logger);
            await BackupTable<ViewerPoint>(context, tempDirectory, logger);
            await BackupTable<ViewerTime>(context, tempDirectory, logger);
            await BackupTable<ViewerMessageCount>(context, tempDirectory, logger);
            await BackupTable<ViewerChatHistory>(context, tempDirectory, logger);
            await BackupTable<DeathCounter>(context, tempDirectory, logger);
            await BackupTable<KeywordType>(context, tempDirectory, logger);
            await BackupTable<Setting>(context, tempDirectory, logger);
            await BackupTable<MusicPlaylist>(context, tempDirectory, logger);
            await BackupTable<SongRequestViewItem>(context, tempDirectory, logger);
            await BackupTable<QuoteType>(context, tempDirectory, logger);
            await BackupTable<RaidHistoryEntry>(context, tempDirectory, logger);
            await BackupTable<AutoShoutout>(context, tempDirectory, logger);
            await BackupTable<TimerGroup>(context, tempDirectory, logger);
            await BackupTable<WordFilter>(context, tempDirectory, logger);
            await BackupTable<SubscriptionHistory>(context, tempDirectory, logger);
            await BackupTable<AliasModel>(context, tempDirectory, logger);
            await BackupTable<KnownBot>(context, tempDirectory, logger);
            await BackupTable<DefaultCommand>(context, tempDirectory, logger);
            await BackupTable<Models.Metrics.SongRequestMetric>(context, tempDirectory, logger);
            await BackupTable<Models.Metrics.SongRequestHistory>(context, tempDirectory, logger);
            await BackupTable<ExternalCommands>(context, tempDirectory, logger);
            await BackupTable<BannedViewer>(context, tempDirectory, logger);
            await BackupTable<RegisteredVoice>(context, tempDirectory, logger);
            await BackupTable<UserRegisteredVoice>(context, tempDirectory, logger);
            await BackupTable<ChannelPointRedeem>(context, tempDirectory, logger);
            await BackupTable<TwitchEvent>(context, tempDirectory, logger);
            await BackupTable<DiscordEventMap>(context, tempDirectory, logger);
            await BackupTable<IpLogEntry>(context, tempDirectory, logger);
            await BackupTable<Wheel>(context, tempDirectory, logger);

            var startPath = tempDirectory;
            var zipPath = Path.Combine(backupDirectory, string.Format("backup-{0}.zip", DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")));
            ZipFile.CreateFromDirectory(startPath, zipPath);
            Directory.Delete(tempDirectory, true);
            logger?.LogInformation($"Backup created at {zipPath}");
        }

        public static async Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            logger?.LogInformation("Restoring database");
            await RestoreTable<GiveawayEntry>(context, backupDirectory, logger);
            await RestoreTable<GiveawayWinner>(context, backupDirectory, logger);
            await RestoreTable<GiveawayExclusion>(context, backupDirectory, logger);
            await RestoreTable<Viewer>(context, backupDirectory, logger);
            await RestoreTable<ViewerTicket>(context, backupDirectory, logger);
            await RestoreTable<Counter>(context, backupDirectory, logger);
            await RestoreTable<CustomCommands>(context, backupDirectory, logger);
            await RestoreTable<AudioCommand>(context, backupDirectory, logger);
            await RestoreTable<ViewerPoint>(context, backupDirectory, logger);
            await RestoreTable<ViewerTime>(context, backupDirectory, logger);
            await RestoreTable<ViewerMessageCount>(context, backupDirectory, logger);
            await RestoreTable<ViewerChatHistory>(context, backupDirectory, logger);
            await RestoreTable<DeathCounter>(context, backupDirectory, logger);
            await RestoreTable<KeywordType>(context, backupDirectory, logger);
            await RestoreTable<Setting>(context, backupDirectory, logger);
            await RestoreTable<SongRequestViewItem>(context, backupDirectory, logger);
            await RestoreTable<MusicPlaylist>(context, backupDirectory, logger);
            await RestoreTable<QuoteType>(context, backupDirectory, logger);
            await RestoreTable<RaidHistoryEntry>(context, backupDirectory, logger);
            await RestoreTable<AutoShoutout>(context, backupDirectory, logger);
            await RestoreTable<TimerGroup>(context, backupDirectory, logger);
            await RestoreTable<WordFilter>(context, backupDirectory, logger);
            await RestoreTable<SubscriptionHistory>(context, backupDirectory, logger);
            await RestoreTable<AliasModel>(context, backupDirectory, logger);
            await RestoreTable<KnownBot>(context, backupDirectory, logger);
            await RestoreTable<DefaultCommand>(context, backupDirectory, logger);
            await RestoreTable<Models.Metrics.SongRequestMetric>(context, backupDirectory, logger);
            await RestoreTable<Models.Metrics.SongRequestHistory>(context, backupDirectory, logger);
            await RestoreTable<ExternalCommands>(context, backupDirectory, logger);
            await RestoreTable<BannedViewer>(context, backupDirectory, logger);
            await RestoreTable<RegisteredVoice>(context, backupDirectory, logger);
            await RestoreTable<UserRegisteredVoice>(context, backupDirectory, logger);
            await RestoreTable<ChannelPointRedeem>(context, backupDirectory, logger);
            await RestoreTable<TwitchEvent>(context, backupDirectory, logger);
            await RestoreTable<DiscordEventMap>(context, backupDirectory, logger);
            await RestoreTable<IpLogEntry>(context, backupDirectory, logger);
            await RestoreTable<Wheel>(context, backupDirectory, logger);
            await context.SaveChangesAsync();
            logger?.LogInformation("Database restored");
        }
    }
}

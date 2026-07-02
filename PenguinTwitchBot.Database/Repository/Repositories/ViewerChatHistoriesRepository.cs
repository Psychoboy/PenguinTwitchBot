
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ViewerChatHistoriesRepository(ApplicationDbContext context) : GenericRepository<ViewerChatHistory>(context), IViewerChatHistoriesRepository
    {
        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            var monthsToKeep = await context.Set<Setting>()
                .Where(x => x.Name == "BackupChatHistoryMonthsToKeep")
                .Select(x => (int?)x.IntSetting)
                .FirstOrDefaultAsync() ?? 12;

            var cutoff = DateTime.UtcNow.AddMonths(-monthsToKeep);
            var query = context.Set<ViewerChatHistory>().AsNoTracking();

            if (monthsToKeep <= 0)
            {
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(x => x.CreatedAt != null && x.CreatedAt > cutoff);
            }

            var fileName = System.IO.Path.Combine(backupDirectory, $"{typeof(ViewerChatHistory).Name}.json");
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = true
            };

            await using var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, bufferSize: 65536, useAsync: true);
            await using var writer = new System.Text.Json.Utf8JsonWriter(fileStream);
            writer.WriteStartArray();

            var count = 0;
            await foreach (var record in query.AsAsyncEnumerable())
            {
                JsonSerializer.Serialize(writer, record, options);
                count++;
                if (count % 500 == 0)
                {
                    await writer.FlushAsync();
                }
            }

            writer.WriteEndArray();
            await writer.FlushAsync();
            logger?.LogDebug("Backed up {Count} filtered records to {Name} using {Months} months retention", count, typeof(ViewerChatHistory).Name, monthsToKeep);
        }
    }
}

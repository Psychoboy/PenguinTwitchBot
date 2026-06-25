using PenguinTwitchBot.Database.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongRequestHistoryWithRankRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistoryWithRank>(context), ISongRequestHistoryWithRankRepository
    {
    
        public override Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
        public override Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }

    }
}

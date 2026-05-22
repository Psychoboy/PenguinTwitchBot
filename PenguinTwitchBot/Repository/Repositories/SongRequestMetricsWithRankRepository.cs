using PenguinTwitchBot.Bot.Models.Metrics;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class SongRequestMetricsWithRankRepository : GenericRepository<SongRequestMetricsWithRank>, ISongRequestMetricsWithRankRepository
    {
        public SongRequestMetricsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }

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

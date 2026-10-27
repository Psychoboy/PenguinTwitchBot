using PenguinTwitchBot.Database.Bot.Models.Metrics;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongRequestMetricsWithRankRepository : GenericRepository<SongRequestMetricsWithRank>, ISongRequestMetricsWithRankRepository
    {
        public SongRequestMetricsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class SongRequestMetricsWithRankRepository : GenericRepository<SongRequestMetricsWithRank>, ISongRequestMetricsWithRankRepository
    {
        public SongRequestMetricsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

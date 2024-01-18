using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestMetricsRepository : GenericRepository<SongRequestMetric>, ISongRequestMetricsRepository
    {
        public SongRequestMetricsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

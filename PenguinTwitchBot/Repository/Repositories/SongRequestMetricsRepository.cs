using PenguinTwitchBot.Bot.Models.Metrics;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class SongRequestMetricsRepository : GenericRepository<SongRequestMetric>, ISongRequestMetricsRepository
    {
        public SongRequestMetricsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

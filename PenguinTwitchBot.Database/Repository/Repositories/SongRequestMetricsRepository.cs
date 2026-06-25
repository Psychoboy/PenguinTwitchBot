using PenguinTwitchBot.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class SongRequestMetricsRepository : GenericRepository<SongRequestMetric>, ISongRequestMetricsRepository
    {
        public SongRequestMetricsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

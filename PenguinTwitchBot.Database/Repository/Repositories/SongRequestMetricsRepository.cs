using PenguinTwitchBot.Database.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongRequestMetricsRepository : GenericRepository<SongRequestMetric>, ISongRequestMetricsRepository
    {
        public SongRequestMetricsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

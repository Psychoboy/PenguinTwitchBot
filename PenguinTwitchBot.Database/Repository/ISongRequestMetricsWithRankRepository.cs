using PenguinTwitchBot.Database.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISongRequestMetricsWithRankRepository : IGenericRepository<SongRequestMetricsWithRank>
    {
    }
}

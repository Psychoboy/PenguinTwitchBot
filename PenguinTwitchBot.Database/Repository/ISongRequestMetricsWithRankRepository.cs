using PenguinTwitchBot.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ISongRequestMetricsWithRankRepository : IGenericRepository<SongRequestMetricsWithRank>
    {
    }
}

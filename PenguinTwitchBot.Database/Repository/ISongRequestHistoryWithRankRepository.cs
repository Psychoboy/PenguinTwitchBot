using PenguinTwitchBot.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ISongRequestHistoryWithRankRepository : IGenericRepository<SongRequestHistoryWithRank>
    {
    }
}

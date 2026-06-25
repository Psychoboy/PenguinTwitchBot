using PenguinTwitchBot.Database.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISongRequestHistoryWithRankRepository : IGenericRepository<SongRequestHistoryWithRank>
    {
    }
}

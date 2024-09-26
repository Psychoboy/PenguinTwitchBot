using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository
{
    public interface ISongRequestHistoryRepository : IGenericRepository<SongRequestHistory>
    {
        Task<List<SongRequestHistoryWithRank>> QuerySongRequestHistoryLimitedByMonths(int numberOfMonths = 1, int? limit = null,
           int? offset = null);
    }
}

using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository
{
    public interface ISongRequestHistoryRepository : IGenericRepository<SongRequestHistory>
    {
        Task<List<SongRequestHistoryWithRank>> QuerySongRequestHistoryLimitedByMonths(int numberOfMonths = 1, int? limit = null,
           int? offset = null);
        Task<int> GetRequestedCountForSong(string songId, int numberOfMonths = 0);
        Task<List<SongRequestHistoryWithRank>> GetTopRequestedSongs(int topN, int numberOfMonths = 0);
        Task<int> CountDistinctSongsLimitedByMonths(int numberOfMonths = 1);
    }
}

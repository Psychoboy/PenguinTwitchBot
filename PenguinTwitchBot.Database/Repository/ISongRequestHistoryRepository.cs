using PenguinTwitchBot.Database.Bot.Models.Metrics;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISongRequestHistoryRepository : IGenericRepository<SongRequestHistory>
    {
        Task<List<SongRequestHistoryWithRank>> QuerySongRequestHistoryLimitedByMonths(int numberOfMonths = 1, int? limit = null,
           int? offset = null);
        Task<int> GetRequestedCountForSong(string songId, int numberOfMonths = 0);
        Task<List<SongRequestHistoryWithRank>> GetTopRequestedSongs(int topN, int numberOfMonths = 1);
        Task<int> CountDistinctSongsLimitedByMonths(int numberOfMonths = 1);
    }
}

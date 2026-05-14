using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestHistoryRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistory>(context), ISongRequestHistoryRepository
    {
        private IQueryable<SongRequestHistory> QueryLimitedByMonths(int numberOfMonths)
        {
            return _context.SongRequestHistories.Where(d =>
                numberOfMonths > 0
                    ? d.RequestDate > DateTime.UtcNow.AddMonths(-numberOfMonths)
                    : d.RequestDate > DateTime.MinValue);
        }

        public async Task<List<SongRequestHistoryWithRank>> QuerySongRequestHistoryLimitedByMonths(int numberOfMonths = 1, int? limit = null, int? offset = null)
        {
            var query = QueryLimitedByMonths(numberOfMonths)
                .GroupBy(c => new
                {
                    c.SongId,
                    c.Duration,
                    c.Title
                })
                .Select(g => new SongRequestHistoryWithRank {
                    Duration = g.Key.Duration,
                    Title = g.Key.Title,
                    SongId = g.Key.SongId,
                    RequestedCount = g.Count(),
                    LastRequestDate = g.Max(x => x.RequestDate)
                })
                .OrderByDescending(o => o.RequestedCount)
                .AsAsyncEnumerable();

            var result = new List<SongRequestHistoryWithRank>();
            int index = 0;

            await foreach (var r in query)
            {
                result.Add(new SongRequestHistoryWithRank
                {
                    Duration = r.Duration,
                    Title = r.Title,
                    SongId = r.SongId,
                    RequestedCount = r.RequestedCount,
                    Ranking = ++index
                });
            }

            if (offset != null)
            {
                result = [.. result.Skip((int)offset)];
            }

            if (limit != null)
            {
                result = [.. result.Take((int)limit)];
            }

            return result;
        }

        public Task<int> GetRequestedCountForSong(string songId, int numberOfMonths = 0)
        {
            return QueryLimitedByMonths(numberOfMonths).CountAsync(x => x.SongId == songId);
        }

        public Task<List<SongRequestHistoryWithRank>> GetTopRequestedSongs(int topN, int numberOfMonths = 1)
        {
            return QuerySongRequestHistoryLimitedByMonths(numberOfMonths, topN, null);
        }

        public Task<int> CountDistinctSongsLimitedByMonths(int numberOfMonths = 1)
        {
            return QueryLimitedByMonths(numberOfMonths)
                .Select(x => x.SongId)
                .Distinct()
                .CountAsync();
        }
    }
}

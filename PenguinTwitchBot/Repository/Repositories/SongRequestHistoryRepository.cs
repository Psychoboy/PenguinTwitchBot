using PenguinTwitchBot.Bot.Models.Metrics;

namespace PenguinTwitchBot.Repository.Repositories
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
            IQueryable<SongRequestHistoryWithRank> query = QueryLimitedByMonths(numberOfMonths)
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
                .OrderByDescending(o => o.RequestedCount);

            int startRank = 1;
            if (offset.HasValue)
            {
                query = query.Skip(offset.Value);
                startRank = offset.Value + 1;
            }

            if (limit.HasValue)
            {
                query = query.Take(limit.Value);
            }

            var rows = await query.ToListAsync();
            int rank = startRank;
            foreach (var r in rows)
            {
                r.Ranking = rank++;
            }

            return rows;
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

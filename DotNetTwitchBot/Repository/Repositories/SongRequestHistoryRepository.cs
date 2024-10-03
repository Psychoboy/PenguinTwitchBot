using DotNetTwitchBot.Bot.Models.Metrics;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestHistoryRepository(ApplicationDbContext context) : GenericRepository<SongRequestHistory>(context), ISongRequestHistoryRepository
    {
        public async Task<List<SongRequestHistoryWithRank>> QuerySongRequestHistoryLimitedByMonths(int numberOfMonths = 1, int? limit = null, int? offset = null)
        {
            var query = _context.SongRequestHistories
                .Where(d => numberOfMonths > 0 ? 
                d.RequestDate > DateTime.Now.AddMonths(-numberOfMonths) : d.RequestDate > DateTime.MinValue)
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
                    RequestedCount = g.Count()
                }).OrderByDescending(o => o.RequestedCount)
                .AsAsyncEnumerable()
                .Select((r, i) => new SongRequestHistoryWithRank
                {
                    Duration = r.Duration,
                    Title= r.Title,
                    SongId = r.SongId,
                    RequestedCount = r.RequestedCount,
                    Ranking = i+1
                });

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }

            return await query.ToListAsync();
        }
    }
}

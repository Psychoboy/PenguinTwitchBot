using PenguinTwitchBot.Bot.Models;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ViewersTimeRepository : GenericRepository<ViewerTime>, IViewersTimeRepository
    {
        public ViewersTimeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<ViewerTimeWithRank?> GetUserTimeWithRankByUsername(string username)
        {
            var normalizedUsername = Bot.Core.UsernameNormalizer.Normalize(username);
            var allViewerTimes = _context.ViewersTime
                .Where(x => x.banned == false)
                .ToLinqToDB();
            return _context.ViewersTime
                .Where(x => x.Username == normalizedUsername && x.banned == false)
                .ToLinqToDB()
                .Select(x => new ViewerTimeWithRank
                {
                    Id = x.Id,
                    Username = x.Username,
                    Time = x.Time,
                    Ranking = allViewerTimes.Count(u => u.Time > x.Time) + 1
                })
                .FirstOrDefaultAsyncLinqToDB();
        }

        public IQueryable<ViewerTimeWithRank> GetRankedTime(Expression<Func<ViewerTimeWithRank, bool>>? filter = null, int? limit = null, int? offset = null)
        {
            var result = _context.ViewersTime
                .Where(x => x.banned == false)
                .ToLinqToDB()
                .Select((x, i) => new ViewerTimeWithRank
                {
                    Id = x.Id,
                    Username = x.Username,
                    Time = x.Time,
                    Ranking = (int)Sql.Ext.Rank().Over().OrderByDesc(x.Time).ToValue()
                });
            var query = result.AsCte();
            if (filter != null)
                query = query.Where(filter);
            query = query.OrderBy(x => x.Ranking);
            if (offset.HasValue)
                query = query.Skip(offset.Value);
            if (limit.HasValue)
                query = query.Take(limit.Value);
            return query;
        }
    }
}

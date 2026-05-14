using DotNetTwitchBot.Bot.Models;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerMessageCountsRepository : GenericRepository<ViewerMessageCount>, IViewerMessageCountsRepository
    {
        public ViewerMessageCountsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<ViewerMessageCountWithRank?> GetUserMessageCountWithRankByUsername(string username)
        {
            var normalizedUsername = Bot.Core.UsernameNormalizer.Normalize(username);
            var allMessageCounts = _context.ViewerMessageCounts
                .Where(x => x.banned == false)
                .ToLinqToDB();
            return _context.ViewerMessageCounts
                .Where(x => x.Username.ToLower() == normalizedUsername && x.banned == false)
                .ToLinqToDB()
                .Select(x => new ViewerMessageCountWithRank
                {
                    Id = x.Id,
                    Username = x.Username,
                    MessageCount = x.MessageCount,
                    Ranking = allMessageCounts.Count(u => u.MessageCount > x.MessageCount) + 1
                })
                .FirstOrDefaultAsyncLinqToDB();
        }

        public IQueryable<ViewerMessageCountWithRank> GetRankedMessageCounts(Expression<Func<ViewerMessageCountWithRank, bool>>? filter = null, int? limit = null, int? offset = null)
        {
            var result = _context.ViewerMessageCounts
                .Where(x => x.banned == false)
                .ToLinqToDB()
                .Select((x, i) => new ViewerMessageCountWithRank
                {
                    Id = x.Id,
                    Username = x.Username,
                    MessageCount = x.MessageCount,
                    Ranking = (int)Sql.Ext.Rank().Over().OrderByDesc(x.MessageCount).ToValue()
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

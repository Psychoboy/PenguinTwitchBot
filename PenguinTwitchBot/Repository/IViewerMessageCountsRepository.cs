using PenguinTwitchBot.Bot.Models;
using System.Linq.Expressions;

namespace PenguinTwitchBot.Repository
{
    public interface IViewerMessageCountsRepository : IGenericRepository<ViewerMessageCount>
    {
        Task<ViewerMessageCountWithRank?> GetUserMessageCountWithRankByUsername(string username);
        IQueryable<ViewerMessageCountWithRank> GetRankedMessageCounts(Expression<Func<ViewerMessageCountWithRank, bool>>? filter = null, int? limit = null, int? offset = null);
    }
}

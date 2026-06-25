using PenguinTwitchBot.Bot.Models;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IViewerMessageCountsRepository : IGenericRepository<ViewerMessageCount>
    {
        Task<ViewerMessageCountWithRank?> GetUserMessageCountWithRankByUsername(string username);
        IQueryable<ViewerMessageCountWithRank> GetRankedMessageCounts(Expression<Func<ViewerMessageCountWithRank, bool>>? filter = null, int? limit = null, int? offset = null);
    }
}

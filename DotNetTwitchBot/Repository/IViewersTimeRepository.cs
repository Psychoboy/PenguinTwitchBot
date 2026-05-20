using DotNetTwitchBot.Bot.Models;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Repository
{
    public interface IViewersTimeRepository : IGenericRepository<ViewerTime>
    {
        Task<ViewerTimeWithRank?> GetUserTimeWithRankByUsername(string username);
        IQueryable<ViewerTimeWithRank> GetRankedTime(Expression<Func<ViewerTimeWithRank, bool>>? filter = null, int? limit = null, int? offset = null);
    }
}

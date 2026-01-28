using DotNetTwitchBot.Bot.Models.Points;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Repository
{
    public interface IUserPointsRepository : IGenericRepository<UserPoints>
    {
        Task<UserPoints?> GetUserPointsByUserId(string userId, PlatformType platform, int pointType);
        Task<UserPointsWithRank?> UserPointsByUserIdWithRank(string userId, PlatformType platform, int pointType);
        IQueryable<UserPointsWithRank> GetRankedPoints(int pointType, Expression<Func<UserPointsWithRank, bool>>? filter = null, Func<IQueryable<UserPointsWithRank>, IOrderedQueryable<UserPointsWithRank>>? orderBy = null, int? limit = null, int? offset = null, string includeProperties = "");
    }
}

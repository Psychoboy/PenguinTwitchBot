using PenguinTwitchBot.Database.Bot.Models.Points;
using System.Linq.Expressions;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IUserPointsRepository : IGenericRepository<UserPoints>
    {
        Task<UserPoints?> GetUserPointsByUserId(string userId, Guid pointType);
        Task<UserPointsWithRank?> UserPointsByUserIdWithRank(string userId, Guid pointType);
        IQueryable<UserPointsWithRank> GetRankedPoints(Guid pointType, Expression<Func<UserPointsWithRank, bool>>? filter = null, int? limit = null, int? offset = null);
    }
}

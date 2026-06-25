using PenguinTwitchBot.Database.Bot.Models.Points;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IUserPointsRepository : IGenericRepository<UserPoints>
    {
        Task<UserPoints?> GetUserPointsByUserId(string userId, int pointType);
        Task<UserPointsWithRank?> UserPointsByUserIdWithRank(string userId, int pointType);
        IQueryable<UserPointsWithRank> GetRankedPoints(int pointType, Expression<Func<UserPointsWithRank, bool>>? filter = null, int? limit = null, int? offset = null);
    }
}

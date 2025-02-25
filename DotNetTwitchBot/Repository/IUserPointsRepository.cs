using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Repository
{
    public interface IUserPointsRepository : IGenericRepository<UserPoints>
    {
        Task<UserPoints?> GetUserPointsByUserId(string userId, int pointType);
    }
}

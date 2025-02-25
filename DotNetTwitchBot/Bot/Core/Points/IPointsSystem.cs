using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface IPointsSystem
    {
        Task AddPointsByUserId(string userId, int pointType, long points);
        Task AddPointsByUsername(string username, int pointType, long points);
        //Task<long> GetMaxPointsByUserId(string userId, int pointType);
        Task<long> GetMaxPointsByUserId(string userId, int pointType, long max);
        Task<UserPoints> GetUserPointsByUserId(string userId, int pointType);
        Task<UserPoints> GetUserPointsByUsername(string username, int pointType);
        Task<PointType?> GetPointTypeById(int pointTypeId);
        Task<IEnumerable<PointType>> GetPointTypes();
        Task AddPointType(PointType pointType);
        Task UpdatePointType(PointType pointType);
        Task DeletePointType(int pointTypeId);
    }
}
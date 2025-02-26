using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface IPointsSystem
    {
        Task AddPointsByUserId(string userId, int pointType, long points);
        Task AddPointsByUsername(string username, int pointType, long points);
        Task AddPointsByUserIdAndGame(string userId, string gameName, long points);
        Task AddPointsByUsernameAndGame(string username, string gameName, long points);
        Task AddPointsToActiveUsers(int pointType, long points);
        Task AddPointsToSubbedUsers(int pointType, long points);
        Task AddPointsToAllCurrentUsers(int pointType, long points);
        //Task<long> GetMaxPointsByUserId(string userId, int pointType);
        Task<long> GetMaxPointsByUserId(string userId, int pointType, long max);
        Task<long> GetMaxPointsByUserIdAndGame(string userId, string gameName, long max);
        Task<UserPoints> GetUserPointsByUserId(string userId, int pointType);
        Task<UserPoints> GetUserPointsByUsername(string username, int pointType);
        Task<UserPoints> GetUserPointsByUserIdAndGame(string userId, string gameName);
        Task<UserPoints> GetUserPointsByUsernameAndGame(string username, string gameName);
        Task<bool> RemovePointsFromUserByUserId(string userId, int pointType, long points);
        Task<bool> RemovePointsFromUserByUsername(string username, int pointType, long points);
        Task<bool> RemovePointsFromUserByUserIdAndGame(string userId, string gameName, long points);
        Task<bool> RemovePointsFromUserByUsernameAndGame(string username, string gameName, long points);
        Task<PointType?> GetPointTypeById(int pointTypeId);
        Task<PointCommand?> GetPointCommand(string pointTypeCommand);
        Task<IEnumerable<PointType>> GetPointTypes();
        Task AddPointType(PointType pointType);
        Task UpdatePointType(PointType pointType);
        Task DeletePointType(int pointTypeId);
        Task<PointType> GetPointTypeForGame(string gameName);
        Task SetPointTypeForGame(string gameName, int pointTypeId);
        Task RunCommand(CommandEventArgs e, PointCommand pointCommand);
    }
}
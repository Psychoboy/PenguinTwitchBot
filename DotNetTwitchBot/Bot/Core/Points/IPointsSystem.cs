using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface IPointsSystem
    {
        Task<long> AddPointsByUserId(string userId, PlatformType platformType, int pointType, long points);
        Task<long> AddPointsByUsername(string username, PlatformType platformType, int pointType, long points);
        Task<long> AddPointsByUserIdAndGame(string userId, PlatformType platformType, string gameName, long points);
        Task<long> AddPointsByUsernameAndGame(string username, PlatformType platformType, string gameName, long points);
        Task AddPointsToActiveUsers(int pointType, PlatformType platformType, long points);
        Task AddPointsToSubbedUsers(int pointType, PlatformType platformType, long points);
        Task AddPointsToAllCurrentUsers(int pointType, PlatformType platformType, long points);
        Task<long> GetMaxPointsByUserId(string userId, PlatformType platformType, int pointType, long max);
        Task<long> GetMaxPointsByUserIdAndGame(string userId, PlatformType platformType, string gameName, long max);
        Task<UserPoints> GetUserPointsByUserId(string userId, PlatformType platformType, int pointType);
        Task<UserPoints> GetUserPointsByUsername(string username, PlatformType platformType, int pointType);
        Task<UserPoints> GetUserPointsByUserIdAndGame(string userId, PlatformType platformType, string gameName);
        Task<UserPoints> GetUserPointsByUsernameAndGame(string username, PlatformType platformType, string gameName);
        Task<bool> RemovePointsFromUserByUserId(string userId, PlatformType platformType, int pointType, long points);
        Task<bool> RemovePointsFromUserByUsername(string username, PlatformType platformType, int pointType, long points);
        Task<bool> RemovePointsFromUserByUserIdAndGame(string userId, PlatformType platformType, string gameName, long points);
        Task<bool> RemovePointsFromUserByUsernameAndGame(string username, PlatformType platformType, string gameName, long points);
        Task RemoveAllPointsForGame(string gameName);
        Task<PointType?> GetPointTypeById(int pointTypeId);
        Task<PointCommand?> GetPointCommand(string pointTypeCommand);
        Task<IEnumerable<PointType>> GetPointTypes();
        Task AddPointType(PointType pointType);
        Task UpdatePointType(PointType pointType);
        Task DeletePointType(int pointTypeId);
        Task<PointType> GetPointTypeForGame(string gameName);
        Task SetPointTypeForGame(string gameName, int pointTypeId);
        Task RegisterDefaultPointForGame(string gameName);
        Task RunCommand(CommandEventArgs e, PointCommand pointCommand);
        Task<PagedDataResponse<LeaderPosition>> GetLeaderPositions(PaginationFilter filter, int pointType);
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name, PlatformType platformType);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name, PlatformType platformType);
        Task<UserPointsWithRank> GetPointsWithRankByUserId(string userId, PlatformType platformType, int pointType);
        Task<UserPointsWithRank> GetPointsWithRankByUsername(string username, PlatformType platformType, int pointType);
        public Task<List<PointGamePair>> GetPointTypesForGames();
    }
}
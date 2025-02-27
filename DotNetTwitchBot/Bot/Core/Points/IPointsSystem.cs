using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface IPointsSystem
    {
        Task<long> AddPointsByUserId(string userId, int pointType, long points);
        Task<long> AddPointsByUsername(string username, int pointType, long points);
        Task<long> AddPointsByUserIdAndGame(string userId, string gameName, long points);
        Task<long> AddPointsByUsernameAndGame(string username, string gameName, long points);
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
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name);
        Task<UserPointsWithRank> GetPointsWithRankByUserId(string userId, int pointType);
        Task<UserPointsWithRank> GetPointsWithRankByUsername(string username, int pointType);
        public Task<List<PointGamePair>> GetAllPointTypes();
    }
}
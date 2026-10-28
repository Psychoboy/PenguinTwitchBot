using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Models;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Core.Points
{
    public interface IPointsSystem
    {
        Task<long> AddPointsByUserId(string userId, Guid pointType, long points);
        Task<bool> AddPointsByUserIdOnce(string userId, Guid pointType, long points, string operationId);
        Task<long> SetPointsByUserId(string userId, Guid pointType, long points);
        Task<long> AddPointsByUsername(string username, Guid pointType, long points);
        Task<long> AddPointsByUserIdAndGame(string userId, string gameName, long points);
        Task<long> AddPointsByUsernameAndGame(string username, string gameName, long points);
        Task AddPointsToActiveUsers(Guid pointType, long points);
        Task AddPointsToSubbedUsers(Guid pointType, long points);
        Task AddPointsToAllCurrentUsers(Guid pointType, long points);
        //Task<long> GetMaxPointsByUserId(string userId, Guid pointType);
        Task<long> GetMaxPointsByUserId(string userId, Guid pointType, long max);
        Task<long> GetMaxPointsByUserIdAndGame(string userId, string gameName, long max);
        Task<UserPoints> GetUserPointsByUserId(string userId, Guid pointType);
        Task<UserPoints> GetUserPointsByUsername(string username, Guid pointType);
        Task<UserPoints> GetUserPointsByUserIdAndGame(string userId, string gameName);
        Task<UserPoints> GetUserPointsByUsernameAndGame(string username, string gameName);
        Task<bool> RemovePointsFromUserByUserId(string userId, Guid pointType, long points);
        Task<bool> RemovePointsFromUserByUsername(string username, Guid pointType, long points);
        Task<bool> RemovePointsFromUserByUserIdAndGame(string userId, string gameName, long points);
        Task<bool> RemovePointsFromUserByUsernameAndGame(string username, string gameName, long points);
        Task RemoveAllPointsForGame(string gameName);
        Task<PointType?> GetPointTypeById(Guid pointTypeId);
        Task<PointCommand?> GetPointCommand(string pointTypeCommand);
        Task<IEnumerable<PointType>> GetPointTypes();
        Task AddPointType(PointType pointType);
        Task UpdatePointType(PointType pointType);
        Task DeletePointType(Guid pointTypeId);
        Task<PointType> GetPointTypeForGame(string gameName);
        Task SetPointTypeForGame(string gameName, Guid pointTypeId);
        Task RegisterDefaultPointForGame(string gameName);
        Task RunCommand(CommandEventArgs e, PointCommand pointCommand);
        Task<PagedDataResponse<LeaderPosition>> GetLeaderPositions(PaginationFilter filter, Guid pointType);
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name);
        Task<UserPointsWithRank> GetPointsWithRankByUserId(string userId, Guid pointType);
        Task<UserPointsWithRank> GetPointsWithRankByUsername(string username, Guid pointType);
        Task<List<PointGamePair>> GetPointTypesForGames();
        Task<List<PointCommand>> GetAllPointCommands();
        Task<PointType?> GetPointTypeByName(string name);
        Task RunFromActionNoResponse(CommandEventArgs e, PointCommand pointCommand, ConcurrentDictionary<string, string> variables);
    }
}
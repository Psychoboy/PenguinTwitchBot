using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingLeaderboardService
    {
        Task<List<LeaderPosition>> GetTotalGoldLeaderboard(int count = 50);
        Task<List<SnapLossLeaderboardPosition>> GetSnapLossLeaderboard(int count = 50);
        Task<List<FishCatch>> GetMostValuableCatchesLeaderboard(int count = 50);
        Task<List<FishCatch>> GetRecentCatches(int count = 50);
        Task<List<FishCatch>> GetUserRecentCatches(string userId, int count = 50);
    }
}

using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingService
    {
        Task<List<FishType>> GetAllFishTypes();
        Task<List<FishType>> GetFishTypesWithCatches();
        Task<FishType?> GetFishTypeById(int id);
        Task AddFishType(FishType fishType);
        Task UpdateFishType(FishType fishType);
        Task DeleteFishType(int id);

        Task<List<FishCatch>> GetTopCatchesForFishType(int fishTypeId, int count = 10);
        Task<List<FishCatch>> GetUserCatches(string userId, int count = 50);
        Task<FishCatch?> GetUserBestCatchForFishType(string userId, int fishTypeId);
        Task<int> GetUserCatchCountForFishType(string userId, int fishTypeId);
        Task<Dictionary<int, FishCatch>> GetUserBestCatchesForAllFishTypes(string userId);
        Task<Dictionary<int, int>> GetUserCatchCountsForAllFishTypes(string userId);

        Task<FishingGold?> GetUserGold(string userId);
        Task AddGoldToUser(string userId, string username, int amount);
        Task RemoveGoldFromUser(string userId, int amount);

        Task<List<FishingShopItem>> GetAllShopItems();
        Task<FishingShopItem?> GetShopItemById(int id);
        Task AddShopItem(FishingShopItem item);
        Task UpdateShopItem(FishingShopItem item);
        Task DeleteShopItem(int id);

        Task<List<UserFishingBoost>> GetUserBoosts(string userId);
        Task<List<UserFishingBoost>> GetUserEquippedItems(string userId);
        Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId);
        Task PurchaseBoost(string userId, int shopItemId);
        Task EquipItem(string userId, int userBoostId);
        Task UnequipItem(string userId, int userBoostId);
        Task ConsumeItemUse(string userId, int userBoostId);

        Task<FishingSettings?> GetSettings();
        Task UpdateSettings(FishingSettings settings);

        Task<FishCatch> PerformFishingAttempt(string userId, string username);

        Task ResetAllUserData();
        Task<int> SyncAllFishRarities();
        Task<int> GenerateDefaultShopItems();

        Task<FishingSimulationResult> SimulateFishing(int iterations, bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);

        Task<List<LeaderPosition>> GetTotalGoldLeaderboard(int count = 50);
        Task<List<FishCatch>> GetMostValuableCatchesLeaderboard(int count = 50);

        Task<Dictionary<string, FishProbability>> CalculateCatchProbabilities(List<int> shopItemIds);
        Task<Dictionary<string, FishProbability>> CalculateCatchProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);
        Task<RarityProbability> CalculateRarityProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);
    }
}

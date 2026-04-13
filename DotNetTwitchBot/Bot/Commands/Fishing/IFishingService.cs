using DotNetTwitchBot.Bot.Models.Fishing;

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
        Task PurchaseBoost(string userId, int shopItemId);

        Task<FishingSettings?> GetSettings();
        Task UpdateSettings(FishingSettings settings);

        Task<FishCatch> PerformFishingAttempt(string userId, string username);

        Task ResetAllUserData();
    }
}

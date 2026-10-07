using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingInventoryService
    {
        Task<List<UserFishingBoost>> GetUserBoosts(string userId);
        Task<List<UserFishingBoost>> GetUserEquippedItems(string userId);
        Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId);
        Task PurchaseBoost(string userId, int shopItemId, int quantity = 1);
        Task GiveItemToUser(string userId, int shopItemId);
        Task SellItem(string userId, int userBoostId);
        Task EquipItem(string userId, int userBoostId);
        Task UnequipItem(string userId, int userBoostId);
        Task ConsumeItemUse(string userId, int userBoostId);
        Task ConsumeItemUses(string userId, IEnumerable<int> userBoostIds);
        Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnReelJam(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnTackleBoxLost(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnNetBreak(string userId, string username);
        Task<List<FishingBrokenItemInfo>> ConsumeItemDurability(string userId, IEnumerable<int> userBoostIds, double repairCostMultiplier);
        int CalculateRepairCost(FishingShopItem shopItem, double currentDurability, double repairCostMultiplier);
        Task<int> RepairItem(string userId, int userBoostId, double? repairCostMultiplier = null, string? username = null);
        Task<int> RepairAllEquippedItems(string userId, double? repairCostMultiplier = null, string? username = null);
    }
}

using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingInventoryService
    {
        Task<List<UserFishingBoost>> GetUserBoosts(string userId);
        Task<List<UserFishingBoost>> GetUserEquippedItems(string userId);
        Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId);
        Task PurchaseBoost(string userId, Guid shopItemId, int quantity = 1);
        Task GiveItemToUser(string userId, Guid shopItemId);
        Task SellItem(string userId, Guid userBoostId);
        Task EquipItem(string userId, Guid userBoostId);
        Task UnequipItem(string userId, Guid userBoostId);
        Task ConsumeItemUse(string userId, Guid userBoostId);
        Task ConsumeItemUses(string userId, IEnumerable<Guid> userBoostIds);
        Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnReelJam(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnTackleBoxLost(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnNetBreak(string userId, string username);
        Task<List<FishingBrokenItemInfo>> ConsumeItemDurability(string userId, IEnumerable<Guid> userBoostIds, double repairCostMultiplier);
        int CalculateRepairCost(FishingShopItem shopItem, double currentDurability, double repairCostMultiplier);
        Task<int> RepairItem(string userId, Guid userBoostId, double? repairCostMultiplier = null, string? username = null);
        Task<int> RepairAllEquippedItems(string userId, double? repairCostMultiplier = null, string? username = null);
    }
}

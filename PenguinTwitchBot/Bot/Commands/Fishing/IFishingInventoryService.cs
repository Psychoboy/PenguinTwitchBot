using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingInventoryService
    {
        Task<List<UserFishingBoost>> GetUserBoosts(string userId);
        Task<List<UserFishingBoost>> GetUserEquippedItems(string userId);
        Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId);
        Task PurchaseBoost(string userId, int shopItemId);
        Task GiveItemToUser(string userId, int shopItemId);
        Task SellItem(string userId, int userBoostId);
        Task EquipItem(string userId, int userBoostId);
        Task UnequipItem(string userId, int userBoostId);
        Task ConsumeItemUse(string userId, int userBoostId);
        Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username);
        Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username);
    }
}

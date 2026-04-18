using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingShopService
    {
        Task<List<FishingShopItem>> GetAllShopItems();
        Task<FishingShopItem?> GetShopItemById(int id);
        Task AddShopItem(FishingShopItem item);
        Task UpdateShopItem(FishingShopItem item);
        Task DeleteShopItem(int id);
        Task<int> GenerateDefaultShopItems();
        Task<int> UpdateShopItemPrices(Dictionary<string, int> priceUpdates);
        Task<int> ApplyPriceMultiplier(double multiplier, bool permanentOnly = false, EquipmentSlot? slot = null);
        Task<Dictionary<int, EquipmentTier>> CalculateDynamicTiers();
        EquipmentTier GetDynamicTier(FishingShopItem item, List<FishingShopItem> allItems);
    }
}

using PenguinTwitchBot.Bot.Models.Fishing;

namespace PenguinTwitchBot.Repository
{
    public interface IFishingRepository : IGenericRepository<FishType>
    {
    }

    public interface IFishCatchRepository : IGenericRepository<FishCatch>
    {
    }

    public interface IFishingGoldRepository : IGenericRepository<FishingGold>
    {
    }

    public interface IFishingShopItemRepository : IGenericRepository<FishingShopItem>
    {
    }

    public interface IUserFishingBoostRepository : IGenericRepository<UserFishingBoost>
    {
    }

    public interface IFishingSettingsRepository : IGenericRepository<FishingSettings>
    {
    }

    public interface IFishingSnapEventRepository : IGenericRepository<FishingSnapEvent>
    {
    }
}
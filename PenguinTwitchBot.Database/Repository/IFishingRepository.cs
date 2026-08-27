using PenguinTwitchBot.Database.Bot.Models.Fishing;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
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

    public interface IFishCategoryRepository : IGenericRepository<FishCategory>
    {
    }

    public interface IFishingTournamentRepository : IGenericRepository<FishingTournament>
    {
    }

    public interface IFishingTournamentFishTypeRepository : IGenericRepository<FishingTournamentFishType>
    {
    }

    public interface IFishingTournamentRewardRuleRepository : IGenericRepository<FishingTournamentRewardRule>
    {
    }

    public interface IFishingTournamentCatchRepository : IGenericRepository<FishingTournamentCatch>
    {
    }

    public interface IFishingTournamentEligibleCategoryRepository : IGenericRepository<FishingTournamentEligibleCategory>
    {
    }
}
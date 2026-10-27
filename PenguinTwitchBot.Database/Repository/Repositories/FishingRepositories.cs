using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class FishingRepository : GenericRepository<FishType>, IFishingRepository
    {
        public FishingRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishingShopItemRepository : GenericRepository<FishingShopItem>, IFishingShopItemRepository
    {
        public FishingShopItemRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishCatchRepository : GenericRepository<FishCatch>, IFishCatchRepository
    {
        public FishCatchRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class UserFishingBoostRepository : GenericRepository<UserFishingBoost>, IUserFishingBoostRepository
    {
        public UserFishingBoostRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishingGoldRepository : GenericRepository<FishingGold>, IFishingGoldRepository
    {
        public FishingGoldRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishingSettingsRepository : GenericRepository<FishingSettings>, IFishingSettingsRepository
    {
        public FishingSettingsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishingSnapEventRepository : GenericRepository<FishingSnapEvent>, IFishingSnapEventRepository
    {
        public FishingSnapEventRepository(ApplicationDbContext context) : base(context)
        {
        }
    }

    public class FishingRepairEventRepository : GenericRepository<FishingRepairEvent>, IFishingRepairEventRepository
    {
        public FishingRepairEventRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

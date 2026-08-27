using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class FishingTournamentRepository(ApplicationDbContext context) : GenericRepository<FishingTournament>(context), IFishingTournamentRepository
    {
    }

    public class FishingTournamentFishTypeRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentFishType>(context), IFishingTournamentFishTypeRepository
    {
    }

    public class FishingTournamentRewardRuleRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentRewardRule>(context), IFishingTournamentRewardRuleRepository
    {
    }

    public class FishingTournamentCatchRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentCatch>(context), IFishingTournamentCatchRepository
    {
    }

    public class FishingTournamentEligibleCategoryRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentEligibleCategory>(context), IFishingTournamentEligibleCategoryRepository
    {
    }

    public class FishCategoryRepository(ApplicationDbContext context) : GenericRepository<FishCategory>(context), IFishCategoryRepository
    {
    }
}
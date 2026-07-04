using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class FishingTournamentRepository(ApplicationDbContext context) : GenericRepository<FishingTournament>(context)
    {
    }

    public class FishingTournamentFishTypeRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentFishType>(context)
    {
    }

    public class FishingTournamentRewardRuleRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentRewardRule>(context)
    {
    }

    public class FishingTournamentCatchRepository(ApplicationDbContext context) : GenericRepository<FishingTournamentCatch>(context)
    {
    }
}
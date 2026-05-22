using PenguinTwitchBot.Bot.Models.Giveaway;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GiveawayExclusionRepository : GenericRepository<GiveawayExclusion>, IGiveawayExclusionRepository
    {
        public GiveawayExclusionRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

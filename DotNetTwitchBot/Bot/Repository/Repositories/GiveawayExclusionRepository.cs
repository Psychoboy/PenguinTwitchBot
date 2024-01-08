using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class GiveawayExclusionRepository : GenericRepository<GiveawayExclusion>, IGiveawayExclusionRepository
    {
        public GiveawayExclusionRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

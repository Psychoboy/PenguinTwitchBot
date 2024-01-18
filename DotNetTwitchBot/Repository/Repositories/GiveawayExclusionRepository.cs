using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class GiveawayExclusionRepository : GenericRepository<GiveawayExclusion>, IGiveawayExclusionRepository
    {
        public GiveawayExclusionRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

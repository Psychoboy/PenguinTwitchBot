using PenguinTwitchBot.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GiveawayExclusionRepository : GenericRepository<GiveawayExclusion>, IGiveawayExclusionRepository
    {
        public GiveawayExclusionRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

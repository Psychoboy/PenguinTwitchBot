using PenguinTwitchBot.Database.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GiveawayExclusionRepository : GenericRepository<GiveawayExclusion>, IGiveawayExclusionRepository
    {
        public GiveawayExclusionRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

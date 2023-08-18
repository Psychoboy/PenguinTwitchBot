using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class GiveawayEntriesRepository : GenericRepository<GiveawayEntry>, IGiveawayEntriesRepository
    {
        public GiveawayEntriesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

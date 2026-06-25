using PenguinTwitchBot.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GiveawayEntriesRepository : GenericRepository<GiveawayEntry>, IGiveawayEntriesRepository
    {
        public GiveawayEntriesRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<int> GetSum()
        {
            return _context.GiveawayEntries.SumAsync(x => x.Tickets);
        }
    }
}

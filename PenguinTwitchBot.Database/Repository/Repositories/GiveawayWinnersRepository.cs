using PenguinTwitchBot.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GiveawayWinnersRepository : GenericRepository<GiveawayWinner>, IGiveawayWinnersRepository
    {
        public GiveawayWinnersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

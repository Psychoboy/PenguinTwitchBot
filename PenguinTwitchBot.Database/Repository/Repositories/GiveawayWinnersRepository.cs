using PenguinTwitchBot.Database.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GiveawayWinnersRepository : GenericRepository<GiveawayWinner>, IGiveawayWinnersRepository
    {
        public GiveawayWinnersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

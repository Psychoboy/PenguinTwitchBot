using PenguinTwitchBot.Bot.Models.Giveaway;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GiveawayWinnersRepository : GenericRepository<GiveawayWinner>, IGiveawayWinnersRepository
    {
        public GiveawayWinnersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

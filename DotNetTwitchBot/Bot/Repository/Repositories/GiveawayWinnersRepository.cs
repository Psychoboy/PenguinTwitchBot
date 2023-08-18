using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class GiveawayWinnersRepository : GenericRepository<GiveawayWinner>, IGiveawayWinnersRepository
    {
        public GiveawayWinnersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class GiveawayWinnersRepository : GenericRepository<GiveawayWinner>, IGiveawayWinnersRepository
    {
        public GiveawayWinnersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

using PenguinTwitchBot.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IGiveawayWinnersRepository : IGenericRepository<GiveawayWinner>
    {
    }
}

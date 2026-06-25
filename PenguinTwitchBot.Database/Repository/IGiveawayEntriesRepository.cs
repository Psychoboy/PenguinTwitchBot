using PenguinTwitchBot.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IGiveawayEntriesRepository : IGenericRepository<GiveawayEntry>
    {
        Task<int> GetSum();
    }
}

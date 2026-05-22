using PenguinTwitchBot.Bot.Models.Giveaway;

namespace PenguinTwitchBot.Repository
{
    public interface IGiveawayEntriesRepository : IGenericRepository<GiveawayEntry>
    {
        Task<int> GetSum();
    }
}

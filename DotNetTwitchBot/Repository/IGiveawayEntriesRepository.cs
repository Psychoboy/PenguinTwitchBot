using DotNetTwitchBot.Bot.Models.Giveaway;

namespace DotNetTwitchBot.Repository
{
    public interface IGiveawayEntriesRepository : IGenericRepository<GiveawayEntry>
    {
        Task<int> GetSum();
    }
}

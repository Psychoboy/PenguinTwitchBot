using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IRaidHistoryRepository : IGenericRepository<RaidHistoryEntry>
    {
    }
}

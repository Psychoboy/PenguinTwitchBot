using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISongCooldownsRepository : IGenericRepository<SongCooldown>
    {
        Task<SongCooldown?> GetBySongIdAsync(string songId);
        Task<List<SongCooldown>> GetActiveCooldownsAsync();
        Task RemoveBySongIdAsync(string songId);
        Task ClearAllAsync();
    }
}

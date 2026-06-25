using PenguinTwitchBot.Database.Bot.Models.Overlay;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IOverlayLayoutRepository : IGenericRepository<OverlayLayout>
    {
        Task<OverlayLayout?> GetByNameAsync(string name);
        Task<OverlayLayout?> GetDefaultAsync();
        Task<List<OverlayLayout>> GetAllWithWidgetsAsync();
        Task<OverlayLayout?> GetByIdWithWidgetsAsync(int id);
    }
}

using PenguinTwitchBot.Database.Bot.Models.Overlay;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IOverlayLayoutRepository : IGenericRepository<OverlayLayout>
    {
        Task<OverlayLayout?> GetByNameAsync(string name);
        Task<OverlayLayout?> GetDefaultAsync();
        Task<List<OverlayLayout>> GetAllWithWidgetsAsync();
        Task<OverlayLayout?> GetByIdWithWidgetsAsync(Guid id);
    }
}

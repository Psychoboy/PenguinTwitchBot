using PenguinTwitchBot.Bot.Models.Overlay;

namespace PenguinTwitchBot.Repository
{
    public interface IOverlayLayoutRepository : IGenericRepository<OverlayLayout>
    {
        Task<OverlayLayout?> GetByNameAsync(string name);
        Task<OverlayLayout?> GetDefaultAsync();
        Task<List<OverlayLayout>> GetAllWithWidgetsAsync();
        Task<OverlayLayout?> GetByIdWithWidgetsAsync(int id);
    }
}

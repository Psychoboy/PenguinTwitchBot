using PenguinTwitchBot.Database.Bot.Models.Overlay;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IOverlayWidgetRepository : IGenericRepository<OverlayWidget>
    {
        Task<List<OverlayWidget>> GetByLayoutIdAsync(Guid layoutId);
    }
}

using PenguinTwitchBot.Bot.Models.Overlay;

namespace PenguinTwitchBot.Repository
{
    public interface IOverlayWidgetRepository : IGenericRepository<OverlayWidget>
    {
        Task<List<OverlayWidget>> GetByLayoutIdAsync(int layoutId);
    }
}

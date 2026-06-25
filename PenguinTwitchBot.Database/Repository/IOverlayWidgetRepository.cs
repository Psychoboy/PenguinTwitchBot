using PenguinTwitchBot.Bot.Models.Overlay;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IOverlayWidgetRepository : IGenericRepository<OverlayWidget>
    {
        Task<List<OverlayWidget>> GetByLayoutIdAsync(int layoutId);
    }
}

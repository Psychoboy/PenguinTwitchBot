using PenguinTwitchBot.Database.Bot.Models.Overlay;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IOverlayWidgetRepository : IGenericRepository<OverlayWidget>
    {
        Task<List<OverlayWidget>> GetByLayoutIdAsync(int layoutId);
    }
}

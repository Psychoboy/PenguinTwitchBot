using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IViewerChatHistoriesRepository : IGenericRepository<ViewerChatHistory>
    {
    }
}

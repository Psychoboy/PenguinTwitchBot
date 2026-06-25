using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IViewerChatHistoriesRepository : IGenericRepository<ViewerChatHistory>
    {
    }
}

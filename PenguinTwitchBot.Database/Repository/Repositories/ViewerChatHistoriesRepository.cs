
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ViewerChatHistoriesRepository(ApplicationDbContext context) : GenericRepository<ViewerChatHistory>(context), IViewerChatHistoriesRepository
    {
    }
}

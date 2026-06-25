
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ViewerChatHistoriesRepository(ApplicationDbContext context) : GenericRepository<ViewerChatHistory>(context), IViewerChatHistoriesRepository
    {
    }
}

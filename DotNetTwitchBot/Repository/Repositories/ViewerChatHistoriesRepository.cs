
namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerChatHistoriesRepository(ApplicationDbContext context) : GenericRepository<ViewerChatHistory>(context), IViewerChatHistoriesRepository
    {
    }
}

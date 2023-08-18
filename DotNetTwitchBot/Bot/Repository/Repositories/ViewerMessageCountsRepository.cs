namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class ViewerMessageCountsRepository : GenericRepository<ViewerMessageCount>, IViewerMessageCountsRepository
    {
        public ViewerMessageCountsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

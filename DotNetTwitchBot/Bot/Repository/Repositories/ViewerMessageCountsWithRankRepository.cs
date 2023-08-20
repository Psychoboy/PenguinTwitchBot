namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class ViewerMessageCountsWithRankRepository : GenericRepository<ViewerMessageCountWithRank>, IViewerMessageCountsWithRankRepository
    {
        public ViewerMessageCountsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

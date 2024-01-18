namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerPointWithRanksRepository : GenericRepository<ViewerPointWithRank>, IViewerPointWithRanksRepository
    {
        public ViewerPointWithRanksRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

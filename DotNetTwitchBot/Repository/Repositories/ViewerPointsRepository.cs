namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerPointsRepository : GenericRepository<ViewerPoint>, IViewerPointsRepository
    {
        public ViewerPointsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

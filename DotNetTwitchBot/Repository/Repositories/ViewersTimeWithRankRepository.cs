namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewersTimeWithRankRepository : GenericRepository<ViewerTimeWithRank>, IViewersTimeWithRankRepository
    {
        public ViewersTimeWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

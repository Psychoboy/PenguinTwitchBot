namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class ViewersTimeRepository : GenericRepository<ViewerTime>, IViewersTimeRepository
    {
        public ViewersTimeRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

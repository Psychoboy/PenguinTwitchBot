namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class FollowerRepository : GenericRepository<Follower>, IFollowerRepository
    {
        public FollowerRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

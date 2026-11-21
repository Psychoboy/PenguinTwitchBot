namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class FollowersRepository : GenericRepository<Follower>, IFollowersRepository
    {
        public FollowersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

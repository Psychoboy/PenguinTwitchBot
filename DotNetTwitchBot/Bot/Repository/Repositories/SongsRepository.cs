namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class SongsRepository : GenericRepository<Song>, ISongsRepository
    {
        public SongsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

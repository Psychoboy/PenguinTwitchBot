namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class SongRequestViewItemsRepository : GenericRepository<SongRequestViewItem>, ISongRequestViewItemsRepository
    {
        public SongRequestViewItemsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

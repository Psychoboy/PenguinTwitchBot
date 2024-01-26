
namespace DotNetTwitchBot.Repository.Repositories
{
    public class TwitchEventsRepository : GenericRepository<TwitchEvent>, ITwitchEventsRepository
    {
        public TwitchEventsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

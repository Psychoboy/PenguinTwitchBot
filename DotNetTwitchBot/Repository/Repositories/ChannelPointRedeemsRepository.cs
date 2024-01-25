
namespace DotNetTwitchBot.Repository.Repositories
{
    public class ChannelPointRedeemsRepository : GenericRepository<ChannelPointRedeem>, IChannelPointRedeemsRepository
    {
        public ChannelPointRedeemsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

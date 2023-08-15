namespace DotNetTwitchBot.Bot.Repository
{
    public class DefaultCommandRepository : GenericRepository<DefaultCommand>, IDefaultCommandRepository
    {
        public DefaultCommandRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

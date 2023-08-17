namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class DefaultCommandRepository : GenericRepository<DefaultCommand>, IDefaultCommandRepository
    {
        public DefaultCommandRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

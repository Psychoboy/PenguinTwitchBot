namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class CustomCommandsRepository : GenericRepository<CustomCommands>, ICustomCommandsRepository
    {
        public CustomCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

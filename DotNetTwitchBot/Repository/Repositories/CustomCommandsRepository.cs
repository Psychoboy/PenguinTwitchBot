using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class CustomCommandsRepository : GenericRepository<CustomCommands>, ICustomCommandsRepository
    {
        public CustomCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

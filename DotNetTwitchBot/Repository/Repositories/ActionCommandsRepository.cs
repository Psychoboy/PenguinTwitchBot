using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ActionCommandsRepository : GenericRepository<ActionCommand>, IActionCommandsRepository
    {
        public ActionCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

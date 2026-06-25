using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ActionCommandsRepository : GenericRepository<ActionCommand>, IActionCommandsRepository
    {
        public ActionCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

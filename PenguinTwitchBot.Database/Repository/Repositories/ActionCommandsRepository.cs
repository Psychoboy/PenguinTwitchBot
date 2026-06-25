using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ActionCommandsRepository : GenericRepository<ActionCommand>, IActionCommandsRepository
    {
        public ActionCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

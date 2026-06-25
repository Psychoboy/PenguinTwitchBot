using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ExternalCommandsRepository : GenericRepository<ExternalCommands>, IExternalCommandsRepository
    {
        public ExternalCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

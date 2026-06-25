using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ExternalCommandsRepository : GenericRepository<ExternalCommands>, IExternalCommandsRepository
    {
        public ExternalCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

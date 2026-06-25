using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class DefaultCommandRepository : GenericRepository<DefaultCommand>, IDefaultCommandRepository
    {
        public DefaultCommandRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

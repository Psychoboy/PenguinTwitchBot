using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class DefaultCommandRepository : GenericRepository<DefaultCommand>, IDefaultCommandRepository
    {
        public DefaultCommandRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

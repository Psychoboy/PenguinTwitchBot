using PenguinTwitchBot.Bot.Models.Commands;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class DefaultCommandRepository : GenericRepository<DefaultCommand>, IDefaultCommandRepository
    {
        public DefaultCommandRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

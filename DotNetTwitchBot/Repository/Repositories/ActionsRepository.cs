using DotNetTwitchBot.Bot.Models.Actions;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ActionsRepository : GenericRepository<ActionType>, IActionsRepository
    {
        public ActionsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

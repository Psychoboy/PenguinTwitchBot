using DotNetTwitchBot.Bot.Models.Actions.SubActions;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SubActionsRepository : GenericRepository<SubActionType>, ISubActionsRepository
    {
        public SubActionsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

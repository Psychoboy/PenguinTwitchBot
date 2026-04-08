using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<int> GetNextIdAsync();
    }
}

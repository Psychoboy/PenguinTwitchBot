using DotNetTwitchBot.Bot.Models.Actions.SubActions;

namespace DotNetTwitchBot.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<int> GetNextIdAsync();
    }
}

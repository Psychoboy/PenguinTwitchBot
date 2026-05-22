using PenguinTwitchBot.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<int> GetNextIdAsync();
    }
}

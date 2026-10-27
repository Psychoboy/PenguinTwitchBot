using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<Guid> GetNextIdAsync();
    }
}

using PenguinTwitchBot.Bot.Actions.SubActions.Types;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<int> GetNextIdAsync();
    }
}

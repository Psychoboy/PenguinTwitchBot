using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ISubActionsRepository : IGenericRepository<SubActionType>
    {
        Task<int> GetNextIdAsync();
    }
}

using DotNetTwitchBot.Bot.Models.Actions;

namespace DotNetTwitchBot.Repository
{
    public interface IActionsRepository : IGenericRepository<ActionType>
    {
        Task<ActionType?> GetByIdWithDetailsAsync(int id);
        Task<List<ActionType>> GetAllWithDetailsAsync();
        Task<ActionType> CreateActionAsync(ActionType action);
        Task<ActionType> UpdateActionAsync(ActionType action);
        Task DeleteActionAsync(int id);
    }
}

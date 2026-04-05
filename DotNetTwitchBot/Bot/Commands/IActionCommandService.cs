using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface IActionCommandService
    {
        Task<List<ActionCommand>> GetAllAsync();
        Task<ActionCommand?> GetByIdAsync(int id);
        Task<ActionCommand> AddAsync(ActionCommand command);
        Task<ActionCommand> UpdateAsync(ActionCommand command);
        Task DeleteAsync(int id);
        Task<bool> CommandExistsAsync(string commandName);
        Task<ActionCommand?> GetByCommandNameAsync(string commandName);
    }
}

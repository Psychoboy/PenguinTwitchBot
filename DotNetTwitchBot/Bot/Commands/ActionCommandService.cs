using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands
{
    public class ActionCommandService : IActionCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommandHelper _commandHelper;

        public ActionCommandService(IUnitOfWork unitOfWork, ICommandHelper commandHelper)
        {
            _unitOfWork = unitOfWork;
            _commandHelper = commandHelper;
        }

        public async Task<List<ActionCommand>> GetAllAsync()
        {
            return await _unitOfWork.ActionCommands.GetAsync(includeProperties: "PointType");
        }

        public async Task<ActionCommand?> GetByIdAsync(int id)
        {
            return await _unitOfWork.ActionCommands.GetByIdAsync(id);
        }

        public async Task<ActionCommand> AddAsync(ActionCommand command)
        {
            // Ensure category is never null
            command.Category = command.Category ?? string.Empty;
            
            await _unitOfWork.ActionCommands.AddAsync(command);
            await _unitOfWork.SaveChangesAsync();
            
            return command;
        }

        public async Task<ActionCommand> UpdateAsync(ActionCommand command)
        {
            // Ensure category is never null
            command.Category = command.Category ?? string.Empty;
            
            _unitOfWork.ActionCommands.Update(command);
            await _unitOfWork.SaveChangesAsync();
            
            return command;
        }

        public async Task DeleteAsync(int id)
        {
            var command = await _unitOfWork.ActionCommands.GetByIdAsync(id);
            if (command != null)
            {
                _unitOfWork.ActionCommands.Remove(command);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<bool> CommandExistsAsync(string commandName)
        {
            return await _commandHelper.CommandExists(commandName);
        }
    }
}

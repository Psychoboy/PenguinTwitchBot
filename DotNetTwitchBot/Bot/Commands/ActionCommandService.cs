using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands
{
    public class ActionCommandService : IActionCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommandHelper _commandHelper;
        private readonly IActionManagementService _actionManagementService;
        private readonly ILogger<ActionCommandService> _logger;

        public ActionCommandService(
            IUnitOfWork unitOfWork, 
            ICommandHelper commandHelper, 
            IActionManagementService actionManagementService,
            ILogger<ActionCommandService> logger)
        {
            _unitOfWork = unitOfWork;
            _commandHelper = commandHelper;
            _actionManagementService = actionManagementService;
            _logger = logger;
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

            // Clear navigation property to avoid EF tracking issues
            command.PointType = null;

            await _unitOfWork.ActionCommands.AddAsync(command);
            await _unitOfWork.SaveChangesAsync();

            return command;
        }

        public async Task<ActionCommand> UpdateAsync(ActionCommand command)
        {
            // Ensure category is never null
            command.Category = command.Category ?? string.Empty;

            // Clear navigation property to avoid EF tracking issues
            command.PointType = null;

            _unitOfWork.ActionCommands.Update(command);
            await _unitOfWork.SaveChangesAsync();

            return command;
        }

        public async Task DeleteAsync(int id)
        {
            var command = await _unitOfWork.ActionCommands.GetByIdAsync(id);
            if (command != null)
            {
                try
                {
                    // Delete all triggers that reference this command
                    await _actionManagementService.DeleteTriggersForCommandAsync(id);
                    _logger.LogInformation("Deleted triggers for command {CommandName} (ID: {CommandId})", command.CommandName, id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting triggers for command {CommandName} (ID: {CommandId})", command.CommandName, id);
                    // Continue with command deletion even if trigger deletion fails
                }

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

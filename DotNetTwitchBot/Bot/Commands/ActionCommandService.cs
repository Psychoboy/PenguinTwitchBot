using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands
{
    public class ActionCommandService(
        IUnitOfWork unitOfWork,
        ICommandHelper commandHelper,
        IActionManagementService actionManagementService,
        ILogger<ActionCommandService> logger) : IActionCommandService
    {
        public async Task<List<ActionCommand>> GetAllAsync()
        {
            return await unitOfWork.ActionCommands.GetAsync(includeProperties: "PointType");
        }

        public async Task<ActionCommand?> GetByIdAsync(int id)
        {
            return await unitOfWork.ActionCommands.GetByIdAsync(id);
        }

        public async Task<ActionCommand?> GetByCommandNameAsync(string commandName)
        {
            var normalizedCommandName = commandName.ToLower();

            var result = await unitOfWork.ActionCommands.GetAsync(
                filter: c => c.CommandName.ToLower() == normalizedCommandName,
                includeProperties: "PointType");
            return result.FirstOrDefault();
        }

        public async Task<ActionCommand> AddAsync(ActionCommand command)
        {
            // Ensure category is never null
            command.Category = command.Category ?? string.Empty;

            // Clear navigation property to avoid EF tracking issues
            command.PointType = null;

            await unitOfWork.ActionCommands.AddAsync(command);
            await unitOfWork.SaveChangesAsync();

            return command;
        }

        public async Task<ActionCommand> UpdateAsync(ActionCommand command)
        {
            // Check if the command name has changed
            if (command.Id.HasValue)
            {
                var existingCommand = await unitOfWork.ActionCommands.Find(x => x.Id == command.Id.Value).AsNoTracking().FirstOrDefaultAsync();
                if (existingCommand != null && existingCommand.CommandName != command.CommandName)
                {
                    // Command name has changed - update trigger configurations and subactions
                    await unitOfWork.Actions.UpdateCommandTriggerConfigurationsForRenamedCommand(command.Id.Value, existingCommand.CommandName, command.CommandName);
                    await unitOfWork.Actions.UpdateToggleCommandDisabledNamesForRenamedCommand(existingCommand.CommandName, command.CommandName);
                }
            }

            // Ensure category is never null
            command.Category = command.Category ?? string.Empty;

            // Clear navigation property to avoid EF tracking issues
            command.PointType = null;

            unitOfWork.ActionCommands.Update(command);
            await unitOfWork.SaveChangesAsync();

            return command;
        }

        public async Task DeleteAsync(int id)
        {
            var command = await unitOfWork.ActionCommands.GetByIdAsync(id);
            if (command != null)
            {
                try
                {
                    // Delete all triggers that reference this command
                    await actionManagementService.DeleteTriggersForCommandAsync(id);
                    logger.LogInformation("Deleted triggers for command {CommandName} (ID: {CommandId})", command.CommandName, id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error deleting triggers for command {CommandName} (ID: {CommandId})", command.CommandName, id);
                    // Continue with command deletion even if trigger deletion fails
                }

                unitOfWork.ActionCommands.Remove(command);
                await unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<bool> CommandExistsAsync(string commandName)
        {
            var actionCommand = await GetByCommandNameAsync(commandName);
            if (actionCommand != null) return true;
            return await commandHelper.CommandExists(commandName);
        }
    }
}

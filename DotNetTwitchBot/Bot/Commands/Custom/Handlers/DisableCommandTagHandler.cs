using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class DisableCommandTagHandler(IServiceScopeFactory serviceScopeFactory, ILogger<DisableCommandTagHandler> logger) : IRequestHandler<DisableCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(DisableCommandTag request, CancellationToken cancellationToken)
        {
            var commands = request.CustomCommand.GetCustomCommands();
            if (commands.TryGetValue(request.Args.Trim(), out var command))
            {
                command.Disabled = true;
                await request.CustomCommand.SaveCommand(command);
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
            var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
            var actionCommandService = scope.ServiceProvider.GetRequiredService<IActionCommandService>();

            var actionCommands = await actionCommandService.GetAllAsync();
            var actionCommand = actionCommands.FirstOrDefault(c =>
                c.CommandName.Equals(request.Args.Trim(), StringComparison.OrdinalIgnoreCase));

            if (actionCommand != null)
            {
                actionCommand.Disabled = true;
                await actionCommandService.UpdateAsync(actionCommand);
                logger.LogInformation("Action command '{CommandName}' has been disabled.", actionCommand.CommandName);
            }

            return new CustomCommandResult();
        }
    }
}

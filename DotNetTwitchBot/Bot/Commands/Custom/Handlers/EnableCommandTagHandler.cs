using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class EnableCommandTagHandler(IServiceScopeFactory serviceScopeFactory, ILogger<EnableCommandTagHandler> logger) : IRequestHandler<EnableCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(EnableCommandTag request, CancellationToken cancellationToken)
        {
            var commands = request.CustomCommand.GetCustomCommands();

            if (commands.TryGetValue(request.Args.Trim(), out var command))
            {
                command.Disabled = false;
                await request.CustomCommand.SaveCommand(command);
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var actionCommandService = scope.ServiceProvider.GetRequiredService<IActionCommandService>();

            var actionCommand = await actionCommandService.GetByCommandNameAsync(request.Args.Trim());

            if (actionCommand != null)
            {
                actionCommand.Disabled = false;
                await actionCommandService.UpdateAsync(actionCommand);
                logger.LogInformation("Action command '{CommandName}' has been enabled.", actionCommand.CommandName);
            }

            return new CustomCommandResult();
        }
    }
}

using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class DisableCommandTagHandler : IRequestHandler<DisableCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(DisableCommandTag request, CancellationToken cancellationToken)
        {
            var commands = request.CustomCommand.GetCustomCommands();
            if (commands.TryGetValue(request.Args.Trim(), out var command))
            {
                command.Disabled = true;
                await request.CustomCommand.SaveCommand(command);
            }
            return new CustomCommandResult();
        }
    }
}

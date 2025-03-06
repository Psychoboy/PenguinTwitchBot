using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class EnableCommandTagHandler : IRequestHandler<EnableCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(EnableCommandTag request, CancellationToken cancellationToken)
        {
            var commands = request.CustomCommand.GetCustomCommands();

            if (commands.TryGetValue(request.Args.Trim(), out var command))
            {
                command.Disabled = false;
                await request.CustomCommand.SaveCommand(command);
            }
            return new CustomCommandResult();
        }
    }
}

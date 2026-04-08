using DotNetTwitchBot.Bot.Commands.Custom.Tags;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class ArgsTagHandler : Application.Notifications.IRequestHandler<ArgsTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(ArgsTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CustomCommandResult(request.CommandEventArgs.Arg.Replace("(", "").Replace(")", "")));
        }
    }
}

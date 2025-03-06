using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class ArgsTagHandler : IRequestHandler<ArgsTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(ArgsTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CustomCommandResult(request.CommandEventArgs.Arg.Replace("(", "").Replace(")", "")));
        }
    }
}

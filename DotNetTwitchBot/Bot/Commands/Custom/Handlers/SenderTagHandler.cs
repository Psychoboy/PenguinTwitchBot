using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class SenderTagHandler : IRequestHandler<SenderTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(SenderTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CustomCommandResult(request.CommandEventArgs.DisplayName));
        }
    }
}

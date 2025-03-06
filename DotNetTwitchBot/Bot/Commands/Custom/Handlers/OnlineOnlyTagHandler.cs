using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class OnlineOnlyTagHandler(IServiceBackbone serviceBackbone) : IRequestHandler<OnlineOnlyTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(OnlineOnlyTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(serviceBackbone.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true));
        }
    }
}

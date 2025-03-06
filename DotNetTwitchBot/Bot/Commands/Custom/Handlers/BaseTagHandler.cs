using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class BaseTagHandler(IServiceBackbone serviceBackbone) : IRequestHandler<BaseTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(BaseTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(serviceBackbone.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true));
        }
    }
}

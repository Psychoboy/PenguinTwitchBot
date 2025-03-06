using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class OfflineOnlyTagHandler(IServiceBackbone serviceBackbone) : IRequestHandler<OfflineOnlyTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(OfflineOnlyTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(serviceBackbone.IsOnline ? new CustomCommandResult(true) : new CustomCommandResult());
        }
    }
}

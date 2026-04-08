using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class OfflineOnlyTagHandler(IServiceBackbone serviceBackbone) : Application.Notifications.IRequestHandler<OfflineOnlyTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(OfflineOnlyTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(serviceBackbone.IsOnline ? new CustomCommandResult(true) : new CustomCommandResult());
        }
    }
}

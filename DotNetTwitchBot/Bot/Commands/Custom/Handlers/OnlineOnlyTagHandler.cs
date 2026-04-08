using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class OnlineOnlyTagHandler(IServiceBackbone serviceBackbone) : Application.Notifications.IRequestHandler<OnlineOnlyTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(OnlineOnlyTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(serviceBackbone.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true));
        }
    }
}

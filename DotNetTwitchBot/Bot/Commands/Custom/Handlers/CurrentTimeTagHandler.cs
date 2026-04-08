using DotNetTwitchBot.Bot.Commands.Custom.Tags;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class CurrentTimeTagHandler : Application.Notifications.IRequestHandler<CurrentTimeTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(CurrentTimeTag request, CancellationToken cancellationToken)
        {
            var time = DateTime.Now.ToString("h:mm:ss tt");
            return Task.FromResult(new CustomCommandResult(time));
        }
    }
}

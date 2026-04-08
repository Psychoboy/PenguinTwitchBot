using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class AlertTagHandler(IPenguinDispatcher mediator) : IRequestHandler<AlertTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(AlertTag request, CancellationToken cancellationToken)
        {
            var alertImage = new AlertImage();
            await mediator.Publish(new QueueAlert(alertImage.Generate(request.Args)), cancellationToken);
            return new CustomCommandResult();
        }
    }
}

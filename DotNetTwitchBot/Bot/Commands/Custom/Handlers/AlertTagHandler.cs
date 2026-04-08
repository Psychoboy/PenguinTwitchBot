using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class AlertTagHandler(Application.Notifications.IPenguinDispatcher dispatcher) : Application.Notifications.IRequestHandler<AlertTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(AlertTag request, CancellationToken cancellationToken)
        {
            var alertImage = new AlertImage();
            await dispatcher.Publish(new QueueAlert(alertImage.Generate(request.Args)), cancellationToken);
            return new CustomCommandResult();
        }
    }
}

using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags.PlayerSound;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class PlaySoundTagHandler(Application.Notifications.IPenguinDispatcher dispatcher) : Application.Notifications.IRequestHandler<PlaySoundTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(PlaySoundTag request, CancellationToken cancellationToken)
        {
            var alertSound = new AlertSound
            {
                AudioHook = request.Args
            };
            await dispatcher.Publish(new QueueAlert(alertSound.Generate()), cancellationToken);
            return new CustomCommandResult();
        }
    }
}

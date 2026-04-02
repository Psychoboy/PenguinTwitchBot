using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class PlaySoundHandler(IMediator mediator, ILogger<PlaySoundHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.PlaySound;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not PlaySoundType playSoundType)
            {
                logger.LogWarning("SubAction with type PlaySound is not of PlaySoundType class");
                return;
            }

            playSoundType.File = VariableReplacer.ReplaceVariables(playSoundType.File, variables);
            var alertSound = new AlertSound { AudioHook = playSoundType.File };
            await mediator.Publish(new QueueAlert(alertSound.Generate()));
        }
    }
}

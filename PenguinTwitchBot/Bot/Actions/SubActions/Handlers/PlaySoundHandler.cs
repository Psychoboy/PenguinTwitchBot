using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Alerts;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class PlaySoundHandler(Application.Notifications.IPenguinDispatcher dispatcher) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.PlaySound;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not PlaySoundType playSoundType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type PlaySound is not of PlaySoundType class");
            }

            playSoundType.File = VariableReplacer.ReplaceVariables(playSoundType.File, variables);
            var alertSound = new AlertSound { AudioHook = playSoundType.File };
            await dispatcher.Publish(new QueueAlert(alertSound.Generate()));
        }
    }
}


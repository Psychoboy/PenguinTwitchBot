using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;
using DotNetTwitchBot.Bot.Queues;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class TtsHandler(    ITTSService ttsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Tts;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if(subAction is not TtsType ttsType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for TTS handler");
            }

            if(string.IsNullOrEmpty(ttsType.Text))
            {
                throw new SubActionHandlerException(subAction, "TTS message is null or empty");
            }

            var message = VariableReplacer.ReplaceVariables(ttsType.Text, variables);
            RegisteredVoice voice;
            if (string.IsNullOrEmpty(ttsType.Name))
            {
                voice = await ttsService.GetRandomVoice();
            } else
            {
                var name = VariableReplacer.ReplaceVariables(ttsType.Name, variables);
                voice = await ttsService.GetRandomVoice(name);
            }
            await ttsService.SayMessage(voice, message);
        }
    }
}


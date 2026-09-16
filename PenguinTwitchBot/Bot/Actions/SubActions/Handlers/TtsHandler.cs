using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Commands.TTS;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
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

            // Resolve the user-specific voice if a name was provided; otherwise pass null and let
            // SayMessage pick a registered voice (or fall back to the system-locale default).
            RegisteredVoice? voice = null;
            if (!string.IsNullOrEmpty(ttsType.Name))
            {
                var name = VariableReplacer.ReplaceVariables(ttsType.Name, variables);
                voice = await ttsService.GetRandomVoice(name);
            }

            await ttsService.SayMessage(voice, message);
        }
    }
}


using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.TTS;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class TtsHandler(ILogger<TtsHandler> logger, ITTSService ttsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Tts;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not TtsType ttsType)
            {
                logger.LogError("Invalid sub action type for TTS handler");
                return;
            }

            if(string.IsNullOrEmpty(ttsType.Text))
            {
                logger.LogError("TTS message is null or empty");
                return;
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

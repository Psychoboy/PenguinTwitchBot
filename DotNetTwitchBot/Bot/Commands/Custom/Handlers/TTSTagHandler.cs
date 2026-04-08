using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.TTS;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class TTSTagHandler(ITTSService ttsService) : Application.Notifications.IRequestHandler<TTSTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(TTSTag request, CancellationToken cancellationToken)
        {
            var voice = await ttsService.GetRandomVoice(request.CommandEventArgs.Name);
            await ttsService.SayMessage(voice, request.Args);
            return new CustomCommandResult();
        }
    }
}

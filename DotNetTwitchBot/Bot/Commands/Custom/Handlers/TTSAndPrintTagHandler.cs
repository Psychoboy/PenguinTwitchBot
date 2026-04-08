using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.TTS;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class TTSAndPrintTagHandler(ITTSService ttsService) : Application.Notifications.IRequestHandler<TTSAndPrintTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(TTSAndPrintTag request, CancellationToken cancellationToken)
        {
            var voice = await ttsService.GetRandomVoice(request.CommandEventArgs.Name);
            await ttsService.SayMessage(voice, request.Args);
            return new CustomCommandResult(request.Args.Replace("(", "").Replace(")", ""));
        }
    }
}


namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public interface ITTSPlayerService
    {
        Task PlayRequest(TTSRequest request);
    }
}
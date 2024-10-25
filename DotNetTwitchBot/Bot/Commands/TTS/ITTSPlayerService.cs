
namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public interface ITTSPlayerService
    {
        Task<string> CreateTTSFile(TTSRequest request);
    }
}
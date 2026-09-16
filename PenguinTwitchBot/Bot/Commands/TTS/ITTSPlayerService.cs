
namespace PenguinTwitchBot.Bot.Commands.TTS
{
    public interface ITTSPlayerService
    {
        Task<string> CreateTTSFile(TTSRequest request);
        void DeleteTTSFile(string fileNameOrRelativeUrl);
        void CleanupOldTTSFiles(TimeSpan maxAge);
    }
}
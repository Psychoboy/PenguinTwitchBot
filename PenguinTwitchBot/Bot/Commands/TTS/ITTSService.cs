using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.TTS
{
    public interface ITTSService
    {
        /// <summary>Returns all available Kokoro voices (always offline/pre-populated).</summary>
        Task<List<RegisteredVoice>> GetAllVoices();

        /// <summary>
        /// Returns all available voices. Pass <paramref name="includeGoogle"/> = <c>true</c>
        /// to additionally fetch Google TTS voices (requires configured credentials).
        /// </summary>
        Task<List<RegisteredVoice>> GetAllVoices(bool includeGoogle);

        Task RegisterVoice(RegisteredVoice voice);
        Task<List<RegisteredVoice>> GetRegisteredVoices();
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task DeleteRegisteredUserVoice(UserRegisteredVoice voice);
        Task DeleteRegisteredVoice(RegisteredVoice voice);
        Task RegisterUserVoice(UserRegisteredVoice voice);
        Task<List<UserRegisteredVoice>> GetUserRegisteredVoices(string username);
        Task<List<UserRegisteredVoice>> GetUserRegisteredVoices();
        Task SayMessage(BaseVoice? voice, string message);
        Task<string> PreviewVoice(BaseVoice voice);
        void DeleteTTSFile(string fileNameOrRelativeUrl);
        Task<BaseVoice?> GetRandomVoice();
        Task<BaseVoice?> GetRandomVoice(string name);
        Task<List<RegisteredVoice>> GetPiperVoices();
        Task<bool> DownloadPiperVoice(string modelKey);
        bool IsPiperVoiceDownloaded(string modelKey);
        Task<int> GetKokoroThreads();
        Task SetKokoroThreads(int threads);
    }
}
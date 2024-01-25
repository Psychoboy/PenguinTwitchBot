using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public interface ITTSService
    {
        Task<List<RegisteredVoice>> GetAllVoices();
        Task RegisterVoice(RegisteredVoice voice);
        Task<List<RegisteredVoice>> GetRegisteredVoices();
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task DeleteRegisteredUserVoice(UserRegisteredVoice voice);
        Task DeleteRegisteredVoice(RegisteredVoice voice);
        Task RegisterUserVoice(UserRegisteredVoice voice);
        Task<List<UserRegisteredVoice>> GetUserRegisteredVoices(string username);
        Task<List<UserRegisteredVoice>> GetUserRegisteredVoices();
        void SayMessage(RegisteredVoice voice, string message);
        Task<RegisteredVoice> GetRandomVoice();
        Task<RegisteredVoice> GetRandomVoice(string name);
    }
}
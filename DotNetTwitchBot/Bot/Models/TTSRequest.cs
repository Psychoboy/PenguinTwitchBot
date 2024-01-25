namespace DotNetTwitchBot.Bot.Models
{
    public class TTSRequest
    {
        public string Message { get; set; } = string.Empty;
        public RegisteredVoice RegisteredVoice { get; set; } = default!;
    }
}

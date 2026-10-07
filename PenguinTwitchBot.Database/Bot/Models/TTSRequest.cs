namespace PenguinTwitchBot.Database.Bot.Models
{
    public class TTSRequest
    {
        public string Message { get; set; } = string.Empty;
        public BaseVoice RegisteredVoice { get; set; } = default!;
    }
}

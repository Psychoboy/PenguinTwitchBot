namespace PenguinTwitchBot.Application.TTS
{
#pragma warning disable S101 // Types should be named in PascalCase
    public class TTSCreateNotification(TTSRequest ttsRequest) : Notifications.INotification
#pragma warning restore S101 // Types should be named in PascalCase
    {
        public TTSRequest TTSRequest { get; private set; } = ttsRequest;
    }
}

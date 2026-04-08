namespace DotNetTwitchBot.Application.TTS
{
    public class TTSCreateNotification(TTSRequest ttsRequest) : Notifications.INotification
    {
        public TTSRequest TTSRequest { get; private set; } = ttsRequest;
    }
}

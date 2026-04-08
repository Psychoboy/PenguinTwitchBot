namespace DotNetTwitchBot.Application.TTS
{
    public class TTSDeleteNotification(string data) : Notifications.INotification
    {
        public string Data { get; private set; } = data;
    }
}

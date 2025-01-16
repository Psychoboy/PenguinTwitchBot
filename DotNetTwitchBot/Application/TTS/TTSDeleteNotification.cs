using MediatR;

namespace DotNetTwitchBot.Application.TTS
{
    public class TTSDeleteNotification(string data) : INotification
    {
        public string Data { get; private set; } = data;
    }
}

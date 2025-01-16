using MediatR;

namespace DotNetTwitchBot.Application.TTS
{
    public class TTSCreateNotification(TTSRequest ttsRequest) : INotification
    {
        public TTSRequest TTSRequest { get; private set; } = ttsRequest;
    }
}

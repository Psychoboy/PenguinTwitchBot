namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models
{
    public class EventSubTransport
    {
        public string Method { get; set; } = default!;
        public string WebsocketId { get; set; } = default!;
    }
}
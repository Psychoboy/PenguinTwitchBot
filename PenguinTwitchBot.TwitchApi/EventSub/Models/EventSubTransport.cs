namespace PenguinTwitchBot.TwitchApi.EventSub.Models
{
    public class EventSubTransport
    {
        public string Method { get; set; } = string.Empty;
        public string? ConduitId { get; set; }
        public string? Callback { get; set; }
        public string? SessionId { get; set; }
    }
}
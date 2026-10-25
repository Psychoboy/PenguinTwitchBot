namespace PenguinTwitchBot.Bot.WebSocketEvents
{
    /// <summary>Client -&gt; server message: <c>{ "request": "subscribe", "topics": ["chat", "alerts"], "multiplex": true }</c>.</summary>
    public class WsSubscriptionRequest
    {
        public string? Request { get; set; }
        public List<string>? Topics { get; set; }
        public string? Id { get; set; }
        public bool? Multiplex { get; set; }
    }
}

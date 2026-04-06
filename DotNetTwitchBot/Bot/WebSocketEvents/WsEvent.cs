namespace DotNetTwitchBot.Bot.WebSocketEvents
{
    public class WsEvent
    {
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public WsEventType Event { get; set; } = new WsEventType();
        public Dictionary<string, object> Data { get; set; } = [];
    }
}
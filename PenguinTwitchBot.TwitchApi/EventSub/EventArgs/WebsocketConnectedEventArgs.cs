namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs
{
    public class WebsocketConnectedEventArgs : System.EventArgs
    {
        public bool IsRequestedReconnect { get; set; }
        public TimeSpan KeepAliveTimeout { get; set; }
    }
}
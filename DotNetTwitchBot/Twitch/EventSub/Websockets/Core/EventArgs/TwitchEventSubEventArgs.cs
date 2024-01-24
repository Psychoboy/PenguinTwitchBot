namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs
{
    public abstract class TwitchEventSubEventArgs<T> : System.EventArgs where T: new()
    {
        public T Notification { get; set; } = new T();
    }
}
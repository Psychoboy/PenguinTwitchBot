namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs
{
    public class ErrorOccuredEventArgs : System.EventArgs
    {
        public Exception? Exception { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
    }
}
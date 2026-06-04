namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs
{
    public class ErrorOccurredEventArgs : System.EventArgs
    {
        public Exception? Exception { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
    }
}
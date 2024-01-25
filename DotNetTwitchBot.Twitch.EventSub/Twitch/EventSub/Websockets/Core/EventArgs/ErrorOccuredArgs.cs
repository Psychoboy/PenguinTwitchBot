namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs
{
    public class ErrorOccuredArgs : System.EventArgs
    {
        public Exception Exception { get; internal set; } = default!;
        public string Message { get; internal set; } = default!;
    }
}
namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs
{
    internal class DataReceivedEventArgs : System.EventArgs
    {
        public required byte[] Bytes { get; internal set; }
    }
}
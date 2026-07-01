namespace PenguinTwitchBot.TwitchApi.EventSub.EventArgs
{
    public class DataReceivedEventArgs : System.EventArgs
    {
        public required byte[] Bytes { get; set; }
    }
}
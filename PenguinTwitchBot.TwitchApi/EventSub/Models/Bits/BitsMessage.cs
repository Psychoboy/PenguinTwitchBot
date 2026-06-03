namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Bits
{
    public class BitsMessage
    {
        public string Text { get; set; } = string.Empty;
        public BitsMessageFragments[] Fragments { get; set; } = [];
    }
}
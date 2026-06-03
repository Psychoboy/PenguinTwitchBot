namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Bits
{
    public class BitsMessageFragments
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public BitsEmote? Emote { get; set; }
        public BitsCheermote? Cheermote { get; set; }
    }
}
namespace PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser
{
    public sealed class FragmentCheermote
    {
        public string Prefix { get; init; } = string.Empty;
        public int Bits { get; init; }
        public int Tier { get; init; }
    }
}
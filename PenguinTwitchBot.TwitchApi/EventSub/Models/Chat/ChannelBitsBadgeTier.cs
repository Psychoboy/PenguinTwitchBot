namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChannelBitsBadgeTier
{
    /// <summary>
    /// The tier of the Bits badge the user just earned. For example, 100, 1000, or 10000.
    /// </summary>
    public int Tier { get; set; }
}
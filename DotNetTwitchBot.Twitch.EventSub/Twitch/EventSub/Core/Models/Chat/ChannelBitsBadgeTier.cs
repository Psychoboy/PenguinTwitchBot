namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the bits badge tier event. Null if notice_type is not bits_badge_tier.
/// </summary>
public sealed class ChannelBitsBadgeTier
{
    /// <summary>
    /// The tier of the Bits badge the user just earned. For example, 100, 1000, or 10000.
    /// </summary>
    public int Tier { get; set; }
}
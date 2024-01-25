namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the gift sub event. Null if notice_type is not sub_gift.
/// </summary>
public sealed class ChatSubGift
{
    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    public int DurationMonths { get; set; }
    /// <summary>
    /// Optional. The amount of gifts the gifter has given in this channel. Null if anonymous.
    /// </summary>
    public int? CumulativeTotal { get; set; }
    /// <summary>
    /// The user ID of the subscription gift recipient.
    /// </summary>
    public string RecipientUserId { get; set; } = string.Empty;
    /// <summary>
    /// The user name of the subscription gift recipient.
    /// </summary>
    public string RecipientUserName { get; set; } = string.Empty;
    /// <summary>
    /// The user login of the subscription gift recipient.
    /// </summary>
    public string RecipientUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    /// <summary>
    /// Optional. The ID of the associated community gift. Null if not associated with a community gift.
    /// </summary>
    public string? CommunityGiftId { get; set; }
}
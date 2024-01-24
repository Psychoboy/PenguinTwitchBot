namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the community gift sub event. Null if notice_type is not community_sub_gift.
/// </summary>
public sealed class ChatCommunitySubGift
{
    /// <summary>
    /// The ID of the associated community gift.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    /// <summary>
    /// Optional. The amount of gifts the gifter has given in this channel. Null if anonymous.
    /// </summary>
    public int? CumulativeTotal { get; set; }
}
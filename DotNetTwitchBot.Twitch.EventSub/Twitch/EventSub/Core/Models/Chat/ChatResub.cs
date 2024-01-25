namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the resub event. Null if notice_type is not resub.
/// </summary>
public sealed class ChatResub
{
    /// <summary>
    /// The total number of months the user has subscribed.
    /// </summary>
    public int CumulativeMonths { get; set; }
    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    public int DurationMonths { get; set; }
    /// <summary>
    /// Optional. The number of consecutive months the user has subscribed.
    /// </summary>
    public int? StreakMonths { get; set; }
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    /// <summary>
    /// Indicates if the resub was obtained through Amazon Prime.
    /// </summary>
    public bool IsPrime { get; set; }
    /// <summary>
    /// Whether or not the resub was a result of a gift.
    /// </summary>
    public bool IsGift { get; set; }
    /// <summary>
    /// Optional. Whether or not the gift was anonymous.
    /// </summary>
    public bool? GifterIsAnonymous { get; set; }
    /// <summary>
    /// Optional. The user ID of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    public string? GifterUserId { get; set; }
    /// <summary>
    /// Optional. The user name of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    public string? GifterUserName { get; set; }
    /// <summary>
    /// Optional. The user login of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    public string? GifterUserLogin { get; set; }
}
namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the sub event. Null if notice_type is not sub.
/// </summary>
public sealed class ChatSub
{
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    public int DurationMonths { get; set; }
    /// <summary>
    /// Indicates if the resub was obtained through Amazon Prime.
    /// </summary>
    public bool IsPrime { get; set; }
}
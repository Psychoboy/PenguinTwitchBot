namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the Prime gift paid upgrade event. Null if notice_type is not prime_paid_upgrade.
/// </summary>
public sealed class ChatPrimePaidUpgrade
{
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
}
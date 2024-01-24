namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Information about the community gift paid upgrade event. Null if notice_type is not gift_paid_upgrade.
/// </summary>
public sealed class ChatGiftPaidUpgrade
{
    /// <summary>
    /// Whether the gift was given anonymously.
    /// </summary>
    public bool GifterIsAnonymous { get; set; }
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
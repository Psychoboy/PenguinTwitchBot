namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Optional. Metadata pertaining to the mention.
/// </summary>
public sealed class ChatMention
{
    /// <summary>
    /// The user ID of the mentioned user.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>
    /// The user name of the mentioned user.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// The user login of the mentioned user.
    /// </summary>
    public string UserLogin { get; set; } = string.Empty;
}
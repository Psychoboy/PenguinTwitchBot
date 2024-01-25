namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Represents a chat badge of a user
/// </summary>
public sealed class ChatBadge
{
    /// <summary>
    /// An ID that identifies this set of chat badges. For example, Bits or Subscriber.
    /// </summary>
    public string SetId { get; set; } = string.Empty;
    /// <summary>
    /// An ID that identifies this version of the badge. The ID can be any value. For example, for Bits, the ID is the Bits tier level, but for World of Warcraft, it could be Alliance or Horde.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// 	Contains metadata related to the chat badges in the badges tag. Currently, this tag contains metadata only for subscriber badges, to indicate the number of months the user has been a subscriber.
    /// </summary>
    public string Info { get; set; } = string.Empty;
}
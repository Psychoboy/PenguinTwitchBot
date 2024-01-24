namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Represents a chat message
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// The chat message in plain text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>
    /// Ordered list of chat message fragments.
    /// </summary>
    public ChatMessageFragment[] Fragments { get; set; } = Array.Empty<ChatMessageFragment>();
}
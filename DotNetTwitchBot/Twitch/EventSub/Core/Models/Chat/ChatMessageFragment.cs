namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// A Fragment of a chat message that holds additional information
/// </summary>
public sealed class ChatMessageFragment
{
    /// <summary>
    /// The type of message fragment. Possible values:
    /// <para>text</para>
    /// <para>cheermote</para>
    /// <para>emote</para>
    /// <para>mention</para>
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    ///	Message text in fragment
    /// </summary>
    public string Text { get; set; } = string.Empty;
    public ChatCheermote? Cheermote { get; set; }
    /// <summary>
    /// Optional. Metadata pertaining to the emote.
    /// </summary>
    public ChatEmote? Emote { get; set; }
    /// <summary>
    /// Optional. Metadata pertaining to the mention.
    /// </summary>
    public ChatMention? Mention { get; set; }
}
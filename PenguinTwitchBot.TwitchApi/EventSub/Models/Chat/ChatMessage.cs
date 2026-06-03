namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public ChatMessageFragment[] Fragments { get; set; } = Array.Empty<ChatMessageFragment>();
}
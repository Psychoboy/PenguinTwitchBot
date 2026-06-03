namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatMessageFragment
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public ChatCheermote? Cheermote { get; set; }
    public ChatEmote? Emote { get; set; }
    
    public ChatMessageMention? Mention { get; set; }
}

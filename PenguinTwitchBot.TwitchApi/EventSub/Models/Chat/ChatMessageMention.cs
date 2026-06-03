namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;
public sealed class ChatMessageMention
{
    public string UserId { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

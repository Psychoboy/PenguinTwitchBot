namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatRaid
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public int ViewerCount { get; set; }
    public string ProfileImageUrl { get; set; } = string.Empty;
}
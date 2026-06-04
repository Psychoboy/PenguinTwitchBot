namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatCommunitySubGift
{
    public string Id { get; set; } = string.Empty;
    public int Total { get; set; }
    public string SubTier { get; set; } = string.Empty;
    public int? CumulativeTotal { get; set; }
}
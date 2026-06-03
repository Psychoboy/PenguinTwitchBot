namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatSubGift
{
    public int DurationMonths { get; set; }
    public int? CumulativeTotal { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public string RecipientUserName { get; set; } = string.Empty;
    public string RecipientUserLogin { get; set; } = string.Empty;
    public string SubTier { get; set; } = string.Empty;
    public string? CommunityGiftId { get; set; }
}
namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public class ChatResub
{
    public int CumulativeMonths { get; set; }
    public int DurationMonths { get; set; }
    public int? StreakMonths { get; set; }

    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// "1000" for Tier 1, "2000" for Tier 2, and "3000" for Tier 3.
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    public bool IsPrime { get; set; }
    public bool IsGift { get; set; }
    public bool? GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
}
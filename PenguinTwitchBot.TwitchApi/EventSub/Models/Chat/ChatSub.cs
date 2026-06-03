namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public class ChatSub
{
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// "1000" for Tier 1, "2000" for Tier 2, and "3000" for Tier 3.
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public bool IsPrime { get; set; }
}

namespace PenguinTwitchBot.Bot.Twitch.Models.ChannelPoints;

public class CreateChannelPointRewardRequest
{
    public string Title { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string? Prompt { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsUserInputRequired { get; set; }
    public bool ShouldRedemptionsSkipRequestQueue { get; set; }
    public bool IsMaxPerStreamEnabled { get; set; }
    public int? MaxPerStream { get; set; }
    public bool IsMaxPerUserPerStreamEnabled { get; set; }
    public int? MaxPerUserPerStream { get; set; }
    public bool IsGlobalCooldownEnabled { get; set; }
    public int? GlobalCooldownSeconds { get; set; }
    public string? BackgroundColor { get; set; }
}

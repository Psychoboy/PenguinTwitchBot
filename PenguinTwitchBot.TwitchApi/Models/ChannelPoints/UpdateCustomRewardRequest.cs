namespace PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

/// <summary>
/// Domain request model for updating an existing channel point reward.
/// </summary>
public sealed class UpdateCustomRewardRequest
{
    public bool? IsEnabled { get; set; }
    public bool? IsPaused { get; set; }
    public string? Title { get; set; }
    public int? Cost { get; set; }
    public string? Prompt { get; set; }
    public string? BackgroundColor { get; set; }
    public bool? IsUserInputRequired { get; set; }
    public bool? ShouldRedemptionsSkipRequestQueue { get; set; }
    public bool? IsMaxPerStreamEnabled { get; set; }
    public int? MaxPerStream { get; set; }
    public bool? IsMaxPerUserPerStreamEnabled { get; set; }
    public int? MaxPerUserPerStream { get; set; }
    public bool? IsGlobalCooldownEnabled { get; set; }
    public int? GlobalCooldownSeconds { get; set; }
}

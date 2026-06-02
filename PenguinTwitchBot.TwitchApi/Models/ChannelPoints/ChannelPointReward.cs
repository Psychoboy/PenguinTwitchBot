namespace PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

/// <summary>
/// Domain model for a channel point custom reward.
/// </summary>
public record ChannelPointReward(
    string Id,
    string Title,
    bool IsEnabled,
    bool IsPaused,
    int Cost,
    string? Prompt,
    bool IsUserInputRequired,
    string? BackgroundColor,
    bool ShouldRedemptionsSkipQueue,
    bool? IsMaxPerStreamEnabled,
    int? MaxPerStream,
    bool? IsMaxPerUserPerStreamEnabled,
    int? MaxPerUserPerStream,
    bool? IsGlobalCooldownEnabled,
    int? GlobalCooldownSeconds);

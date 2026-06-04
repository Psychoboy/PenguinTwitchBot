namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain response model for AutoMod checks.
/// </summary>
public sealed record CheckAutoModStatusResponse(
    IReadOnlyList<AutoModResult> Data);

namespace PenguinTwitchBot.TwitchApi.Models.Clips;

/// <summary>
/// Domain response model for clips.
/// </summary>
public sealed record GetClipsResponse(
    IReadOnlyList<Clip> Data);
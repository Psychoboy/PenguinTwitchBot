namespace PenguinTwitchBot.TwitchApi.Models.Games;

/// <summary>
/// Domain response model for games.
/// </summary>
public sealed record GetGamesResponse(
    IReadOnlyList<Game> Data);
namespace PenguinTwitchBot.TwitchApi.Models.Streams;

/// <summary>
/// Domain response model for live streams.
/// </summary>
public sealed record GetStreamsResponse(
    IReadOnlyList<Stream> Data);
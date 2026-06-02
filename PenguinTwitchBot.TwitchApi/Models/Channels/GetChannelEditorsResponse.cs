namespace PenguinTwitchBot.TwitchApi.Models.Channels;

/// <summary>
/// Domain response model for channel editors.
/// </summary>
public sealed record GetChannelEditorsResponse(
    IReadOnlyList<ChannelEditor> Data);
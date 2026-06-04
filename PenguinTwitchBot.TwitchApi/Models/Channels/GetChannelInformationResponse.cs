namespace PenguinTwitchBot.TwitchApi.Models.Channels;

/// <summary>
/// Domain response model for channel information.
/// </summary>
public sealed record GetChannelInformationResponse(
    IReadOnlyList<ChannelInformation> Data);
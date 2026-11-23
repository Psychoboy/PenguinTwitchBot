namespace PenguinTwitchBot.TwitchApi.Models.Teams;

public sealed record GetChannelTeamsResponse(IReadOnlyList<ChannelTeam> Data);

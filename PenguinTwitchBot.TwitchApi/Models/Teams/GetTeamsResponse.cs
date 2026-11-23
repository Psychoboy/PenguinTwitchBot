namespace PenguinTwitchBot.TwitchApi.Models.Teams;

public sealed record GetTeamsResponse(IReadOnlyList<Team> Data);

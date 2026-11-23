namespace PenguinTwitchBot.TwitchApi.Models.Teams;

public sealed record Team(
    string Id,
    string TeamName,
    string TeamDisplayName,
    IReadOnlyList<TeamMember> Users);

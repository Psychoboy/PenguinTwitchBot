namespace PenguinTwitchBot.TwitchApi.Models.Teams;

public sealed record ChannelTeam(
    string Id,
    string TeamName,
    string TeamDisplayName,
    string BroadcasterId,
    string BroadcasterLogin,
    string BroadcasterName);

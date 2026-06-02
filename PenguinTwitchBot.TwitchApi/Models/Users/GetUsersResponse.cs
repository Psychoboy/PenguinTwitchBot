namespace PenguinTwitchBot.TwitchApi.Models.Users;

/// <summary>
/// Domain response model for the Twitch users lookup endpoint.
/// </summary>
public sealed record GetUsersResponse(
    IReadOnlyList<User> Users);
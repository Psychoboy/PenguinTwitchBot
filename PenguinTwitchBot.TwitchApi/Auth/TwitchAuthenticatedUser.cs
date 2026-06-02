namespace PenguinTwitchBot.TwitchApi.Auth;

public sealed class TwitchAuthenticatedUser
{
    public required string Id { get; init; }
    public required string Login { get; init; }
    public required string DisplayName { get; init; }
    public required string ProfileImageUrl { get; init; }
}

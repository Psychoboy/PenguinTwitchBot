namespace PenguinTwitchBot.Models;

public sealed class TwitchExtensionSession
{
    public required string SessionKey { get; init; }
    public string? UserId { get; set; }
    public required string OpaqueUserId { get; init; }
    public string Username { get; set; } = "Anonymous";
    public string Role { get; set; } = "viewer";
    public bool IsIdentityShared { get; set; }
    public string? UserIp { get; set; }
    public string LastFeature { get; set; } = "features";
    public string LastEndpoint { get; set; } = string.Empty;
    public DateTime FirstSeenUtc { get; init; }
    public DateTime LastSeenUtc { get; set; }
    public long RequestCount { get; set; }

    public string ShortOpaqueId =>
        string.IsNullOrWhiteSpace(OpaqueUserId)
            ? "unknown"
            : OpaqueUserId.Length <= 10
                ? OpaqueUserId
                : OpaqueUserId[..10] + "…";
}

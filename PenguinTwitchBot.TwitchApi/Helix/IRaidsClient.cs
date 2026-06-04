namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IRaidsClient
{
    Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId);
}

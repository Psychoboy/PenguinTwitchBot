namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IRaidsTransport
{
    Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId);
}

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IRaidsTransport
{
    Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId);
}

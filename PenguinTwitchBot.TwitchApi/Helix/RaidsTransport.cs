namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class RaidsTransport : IRaidsTransport
{
    public async Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"raids?from_broadcaster_id={Uri.EscapeDataString(broadcasterId)}&to_broadcaster_id={Uri.EscapeDataString(targetUserId)}");
        using var response = await http.PostAsync(url, content: null);
        response.EnsureSuccessStatusCode();
    }
}

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class RaidsTransport : IRaidsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RaidsTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartRaidAsync(string clientId, string? accessToken, string broadcasterId, string targetUserId)
    {
        using var http = HelixHttp.CreateClient(_httpClientFactory, clientId, accessToken);
        var url = HelixQuery.Build("raids", new (string Key, string? Value)[]
        {
            ("from_broadcaster_id", broadcasterId),
            ("to_broadcaster_id", targetUserId)
        });
        using var response = await http.PostAsync(url, content: null);
        response.EnsureSuccessStatusCode();
    }
}

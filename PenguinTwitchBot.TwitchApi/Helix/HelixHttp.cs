using System.Net.Http.Headers;

namespace PenguinTwitchBot.TwitchApi.Helix;

internal static class HelixHttp
{
    internal const string HelixClientName = "TwitchHelix";
    private static readonly Uri HelixBaseUri = new("https://api.twitch.tv/helix/");

    internal static HttpClient CreateClient(IHttpClientFactory httpClientFactory, string clientId, string? accessToken)
    {
        var client = httpClientFactory.CreateClient(HelixClientName);
        if (client.BaseAddress is null)
        {
            // Fallback for call paths that resolve an unregistered named client.
            client.BaseAddress = HelixBaseUri;
        }

        client.DefaultRequestHeaders.Add("Client-Id", clientId);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }
}

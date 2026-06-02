using System.Net.Http.Headers;

namespace PenguinTwitchBot.TwitchApi.Helix;

internal static class HelixHttp
{
    private const string HelixBaseUrl = "https://api.twitch.tv/helix/";

    internal static string BuildUrl(string relativePath)
    {
        return new Uri(new Uri(HelixBaseUrl), relativePath).ToString();
    }

    internal static HttpClient CreateClient(string clientId, string? accessToken)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(HelixBaseUrl),
        };

        client.DefaultRequestHeaders.Add("Client-Id", clientId);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }
}

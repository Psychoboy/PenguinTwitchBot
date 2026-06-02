using System.Net.Http.Headers;

namespace PenguinTwitchBot.TwitchApi.Helix;

internal static class HelixHttp
{
    internal const string HelixClientName = "TwitchHelix";

    internal static HttpClient CreateClient(IHttpClientFactory httpClientFactory, string clientId, string? accessToken)
    {
        var client = httpClientFactory.CreateClient(HelixClientName);

        client.DefaultRequestHeaders.Add("Client-Id", clientId);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }
}

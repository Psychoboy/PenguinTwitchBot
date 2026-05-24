using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Setup.Services;

public class DiscordLookupService(IHttpClientFactory httpClientFactory)
{
    private const string ApiBase = "https://discord.com/api/v10";

    private HttpClient CreateClient(string token)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("PenguinTwitchBot/1.0");
        return client;
    }

    public async Task<List<(string Id, string Name)>> GetGuildsAsync(string token)
    {
        var client = CreateClient(token);
        var response = await client.GetAsync($"{ApiBase}/users/@me/guilds");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<DiscordGuildJson>>() ?? [];
        return items.Select(g => (g.Id, g.Name)).ToList();
    }

    public async Task<List<(string Id, string Name)>> GetTextChannelsAsync(string token, string guildId)
    {
        var client = CreateClient(token);
        var response = await client.GetAsync($"{ApiBase}/guilds/{guildId}/channels");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<DiscordChannelJson>>() ?? [];
        return items
            .Where(c => c.Type == 0 || c.Type == 5)
            .OrderBy(c => c.Position)
            .Select(c => (c.Id, c.Name))
            .ToList();
    }

    public async Task<List<(string Id, string Name)>> GetRolesAsync(string token, string guildId)
    {
        var client = CreateClient(token);
        var response = await client.GetAsync($"{ApiBase}/guilds/{guildId}/roles");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<DiscordRoleJson>>() ?? [];
        return items
            .Where(r => r.Name != "@everyone")
            .OrderByDescending(r => r.Position)
            .Select(r => (r.Id, r.Name))
            .ToList();
    }

    private record DiscordGuildJson(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name
    );

    private record DiscordChannelJson(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] int Type,
        [property: JsonPropertyName("position")] int Position
    );

    private record DiscordRoleJson(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("position")] int Position
    );
}

using PenguinTwitchBot.TwitchApi.Models.Users;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class UsersTransport : IUsersTransport
{
    public async Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl(BuildUsersUrl(userIds, logins));
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetUsersApiResponse>(response);
        var users = payload?.Data.Select(MapToUser).ToList() ?? [];
        return new GetUsersResponse(users);
    }

    private static string BuildUsersUrl(List<string>? userIds, List<string>? logins)
    {
        var queryParts = new List<string>();

        if (userIds != null)
        {
            foreach (var userId in userIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                queryParts.Add($"id={Uri.EscapeDataString(userId)}");
            }
        }

        if (logins != null)
        {
            foreach (var login in logins.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                queryParts.Add($"login={Uri.EscapeDataString(login)}");
            }
        }

        return queryParts.Count == 0
            ? "users"
            : $"users?{string.Join("&", queryParts)}";
    }

    private static User MapToUser(GetUsersApiItem source)
    {
        return new User(
            Id: source.Id,
            Login: source.Login,
            DisplayName: source.DisplayName,
            Description: source.Description ?? string.Empty,
            CreatedAt: source.CreatedAt,
            ProfileImageUrl: source.ProfileImageUrl,
            OfflineImageUrl: source.OfflineImageUrl,
            Email: source.Email,
            Type: source.Type);
    }

    private sealed record GetUsersApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<GetUsersApiItem> Data);

    private sealed record GetUsersApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("profile_image_url")] string? ProfileImageUrl,
        [property: JsonPropertyName("offline_image_url")] string? OfflineImageUrl,
        [property: JsonPropertyName("email")] string? Email);
}

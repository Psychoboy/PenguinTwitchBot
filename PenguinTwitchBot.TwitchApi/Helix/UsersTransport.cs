using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class UsersTransport : IUsersTransport
{
    public async Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = BuildUsersUrl(userIds, logins);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetUsersResponse>(response);
        return payload ?? new GetUsersResponse();
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
}

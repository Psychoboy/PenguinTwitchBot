using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class UsersTransport : IUsersTransport
{
    public Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            api.Settings.AccessToken = accessToken;
        }

        return api.Helix.Users.GetUsersAsync(userIds, logins, accessToken);
    }
}

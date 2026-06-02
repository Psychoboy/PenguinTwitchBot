using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class UsersClient(ILogger<UsersClient> logger, IUsersTransport transport) : TwitchClientRetryBase(logger), IUsersClient
{

    public Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins)
    {
        return ExecuteWithRetryAsync(
            () => transport.GetUsersAsync(clientId, accessToken, userIds, logins),
            "fetch users");
    }

}

using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLibUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class UsersClient(ILogger<UsersClient> logger, IUsersTransport transport) : TwitchClientRetryBase(logger), IUsersClient
{

    public Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins)
    {
        return ExecuteWithRetryAsync(
            () => transport.GetUsersAsync(clientId, accessToken, userIds, logins),
            "fetch users");
    }

    /// <summary>
    /// Maps a TwitchLib User to the internal domain model
    /// </summary>
    public static Models.Users.User MapToUser(TwitchLibUser source)
    {
        return new Models.Users.User(
            Id: source.Id,
            Login: source.Login,
            DisplayName: source.DisplayName,
            Description: source.Description,
            CreatedAt: source.CreatedAt,
            ProfileImageUrl: source.ProfileImageUrl,
            OfflineImageUrl: source.OfflineImageUrl,
            Email: source.Email,
            Type: source.Type);
    }
}

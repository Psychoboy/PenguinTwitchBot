using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IUsersClient
{
    Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins);
}

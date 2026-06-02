using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IUsersClient
{
    Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins);
}

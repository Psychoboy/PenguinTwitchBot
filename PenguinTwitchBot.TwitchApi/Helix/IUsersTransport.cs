using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IUsersTransport
{
    Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins);
}

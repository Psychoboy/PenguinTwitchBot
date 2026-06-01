using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IUsersClient
{
    Task<GetUsersResponse> GetUsersAsync(string clientId, string? accessToken, List<string>? userIds, List<string>? logins);
}

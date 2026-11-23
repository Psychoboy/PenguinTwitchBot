namespace PenguinTwitchBot.TwitchApi.Models.Teams;

public sealed record TeamMember(
    string UserId,
    string UserLogin,
    string UserName);

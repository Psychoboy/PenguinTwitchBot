namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain request model for banning or timing out a user.
/// </summary>
public class BanUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? Duration { get; set; }
}

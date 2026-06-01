namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a channel subscription.
/// </summary>
public record Subscription(
    string UserId,
    string UserLogin,
    string UserName);

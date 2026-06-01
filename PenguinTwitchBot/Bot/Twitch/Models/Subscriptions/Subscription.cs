namespace PenguinTwitchBot.Bot.Twitch.Models.Subscriptions;

/// <summary>
/// Domain model for a channel subscription.
/// </summary>
public record Subscription(
    string UserId,
    string UserLogin,
    string UserName);

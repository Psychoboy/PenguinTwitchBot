namespace PenguinTwitchBot.TwitchApi.Models.Subscriptions;

/// <summary>
/// Domain model for a channel subscription.
/// </summary>
public record Subscription(
    string UserId,
    string UserLogin,
    string UserName,
    string Tier = "",
    bool IsGift = false,
    string? GifterId = null,
    string? GifterLogin = null,
    string? GifterName = null,
    string PlanName = "");

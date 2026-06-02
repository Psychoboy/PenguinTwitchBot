namespace PenguinTwitchBot.TwitchApi.Models.Subscriptions;

/// <summary>
/// Domain response model for checking a single user's subscription.
/// </summary>
public sealed record CheckUserSubscriptionResponse(
    IReadOnlyList<Subscription> Data);
namespace PenguinTwitchBot.TwitchApi.Models.Subscriptions;

/// <summary>
/// Domain response model for broadcaster subscriptions.
/// </summary>
public sealed record GetBroadcasterSubscriptionsResponse(
    IReadOnlyList<Subscription> Data,
    string? Cursor,
    int Total,
    int Points);
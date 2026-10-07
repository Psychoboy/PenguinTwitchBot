namespace PenguinTwitchBot.TwitchApi.Models.EventSub;

/// <summary>
/// Result of creating an EventSub subscription, including the subscription id and
/// the raw Twitch error body when rejected (for diagnosis).
/// </summary>
public record CreateEventSubSubscriptionResult(bool IsEnabled, string? SubscriptionId, string? Error);

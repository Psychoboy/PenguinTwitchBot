namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Subscriptions;

public sealed class SubscriptionMessage
{
    public string Text { get; set; } = string.Empty;
    public SubscriptionMessageEmote[] Emotes { get; set; } = Array.Empty<SubscriptionMessageEmote>();
}
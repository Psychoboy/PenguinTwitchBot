namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Subscriptions;

public sealed class SubscriptionMessageEmote
{
    public int Begin { get; set; }
    public int End { get; set; }
    public string Id { get; set; } = string.Empty;
}
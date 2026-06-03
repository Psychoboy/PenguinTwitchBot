namespace PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser
{
    public sealed class SuspiciousUserMessage
    {
        public string MessageId { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public MessageFragment[] Fragments { get; init; } = [];
    }
}
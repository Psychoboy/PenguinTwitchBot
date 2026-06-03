using PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel
{
    public sealed class ChannelSuspiciousUserMessage  : ChannelSuspiciousUserBase
    {
        public string[] SharedBanChannelIds { get; init; } = [];
        public string[] Types { get; init; } = [];
        public string BanEvasionEvaluation { get; init; } = string.Empty;
        public SuspiciousUserMessage Message { get; init; } = new();
    }
}
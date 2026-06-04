using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Models
{
    public class WebsocketEventSubMetaData : EventSubMetadata
    {
        

        /// <summary>
        /// The subscription type.
        /// </summary>
        public string? SubscriptionType { get; set; }

        /// <summary>
        /// The subscription version.
        /// </summary>
        public string? SubscriptionVersion { get; set; }
        [MemberNotNullWhen(true, nameof(SubscriptionType), nameof(SubscriptionVersion))]
        public bool HasSubscriptionInfo => SubscriptionType is not null && SubscriptionVersion is not null;
    }
}
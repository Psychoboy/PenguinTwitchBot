using DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;
using DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Core.Models.Chat;

namespace DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Core.SubscriptionTypes.Channel
{
    public sealed class ChannelChatMessage
    {
        /// <summary>
        /// The broadcaster user ID.
        /// </summary>
        public string BroadcasterUserId { get; set; } = string.Empty;
        /// <summary>
        /// The broadcaster display name.
        /// </summary>
        public string BroadcasterUserName { get; set; } = string.Empty;
        /// <summary>
        /// The broadcaster login.
        /// </summary>
        public string BroadcasterUserLogin { get; set; } = string.Empty;
        /// <summary>
        /// The user ID of the user that sent the message.
        /// </summary>
        public string ChatterUserId { get; set; } = string.Empty;
        /// <summary>
        /// The user name of the user that sent the message.
        /// </summary>
        public string ChatterUserName { get; set; } = string.Empty;
        /// <summary>
        /// The user login of the user that sent the message.
        /// </summary>
        public string ChatterUserLogin { get; set; } = string.Empty;
        /// <summary>
        /// A UUID that identifies the message.
        /// </summary>
        public string MessageId { get; set; } = string.Empty;
        /// <summary>
        /// The structured chat message
        /// </summary>
        public ChatMessage Message { get; set; } = new();
        /// <summary>
        /// The color of the user’s name in the chat room.
        /// </summary>
        public string Color { get; set; } = string.Empty;
        /// <summary>
        /// Array of chat badges.
        /// </summary>
        public ChatBadge[] Badges { get; set; } = [];

        /// <summary>The type of message. Possible values:</summary>
        /// <para>text</para>
        /// <para>channel_points_highlighted</para>
        /// <para>channel_points_sub_only</para>
        /// <para>user_intro</para>
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// Metadata if this message is a cheer.
        /// </summary>
        public ChatCheer? Cheer { get; set; }

        /// <summary>Metadata if this message is a reply.</summary>
        public ChatReply? Reply { get; set; }

        /// <summary>
        /// Optional. The ID of a channel points custom reward that was redeemed.
        /// </summary>
        public string ChannelPointsCustomRewardId { get; set; } = string.Empty;

        public bool IsSubscriber => Badges.Where(x => x.SetId.Equals("subscriber", StringComparison.CurrentCultureIgnoreCase)).Any();

        public bool IsModerator => Badges.Where(x => x.SetId.Equals("moderator", StringComparison.CurrentCultureIgnoreCase)).Any();
        public bool IsBroadcaster => Badges.Where(x => x.SetId.Equals("broadcaster", StringComparison.CurrentCultureIgnoreCase)).Any();
        public bool IsVip => Badges.Where(x => x.SetId.Equals("vip", StringComparison.CurrentCultureIgnoreCase)).Any();
    }
}

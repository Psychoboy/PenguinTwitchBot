using DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Chat Notification subscription type model
/// <para>Description:</para>
/// <para>An event that appears in chat occurs, such as someone subscribing to the channel or a subscription is gifted.</para>
/// </summary>
public sealed class ChannelChatNotification
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
    /// Whether or not the chatter is anonymous.
    /// </summary>
    public bool ChatterIsAnonymous { get; set; }

    /// <summary>
    /// The color of the user’s name in the chat room.
    /// </summary>
    public string Color { get; set; } = string.Empty;
    /// <summary>
    /// Array of chat badges.
    /// </summary>
    public ChatBadge[] Badges { get; set; } = Array.Empty<ChatBadge>();
    /// <summary>
    /// The message Twitch shows in the chat room for this notice
    /// </summary>
    public string SystemMessage { get; set; } = string.Empty;
    /// <summary>
    /// A UUID that identifies the message.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>
    /// The structured chat message
    /// </summary>
    public ChatMessage Message { get; set; } = new();
    /// <summary>
    /// The type of notice. Possible values are:
    /// <para>sub</para>
    /// <para>resub</para>
    /// <para>sub_gift</para>
    /// <para>community_sub_gift</para>
    /// <para>gift_paid_upgrade</para>
    /// <para>prime_paid_upgrade</para>
    /// <para>raid</para>
    /// <para>unraid</para>
    /// <para>pay_it_forward</para>
    /// <para>announcement</para>
    /// <para>bits_badge_tier</para>
    /// <para>charity_donation</para>
    /// </summary>
    public string NoticeType { get; set; } = string.Empty;
    /// <summary>
    /// Information about the sub event. Null if notice_type is not sub.
    /// </summary>
    public ChatSub? Sub { get; set; }
    /// <summary>
    /// Information about the resub event. Null if notice_type is not resub.
    /// </summary>
    public ChatResub? Resub { get; set; }
    /// <summary>
    /// Information about the gift sub event. Null if notice_type is not sub_gift.
    /// </summary>
    public ChatSubGift? SubGift { get; set; }
    /// <summary>
    /// Information about the community gift sub event. Null if notice_type is not community_sub_gift.
    /// </summary>
    public ChatCommunitySubGift? CommunitySubGift { get; set; }
    /// <summary>
    /// Information about the community gift paid upgrade event. Null if notice_type is not gift_paid_upgrade.
    /// </summary>
    public ChatGiftPaidUpgrade? GiftPaidUpgrade { get; set; }
    /// <summary>
    /// Information about the Prime gift paid upgrade event. Null if notice_type is not prime_paid_upgrade.
    /// </summary>
    public ChatPrimePaidUpgrade? PrimePaidUpgrade { get; set; }
    /// <summary>
    /// Information about the raid event. Null if notice_type is not raid.
    /// </summary>
    public ChatRaid? Raid { get; set; }
    /// <summary>
    /// Returns an empty payload if notice_type is unraid, otherwise returns null.
    /// </summary>
    public ChatUnraid? Unraid { get; set; }
    /// <summary>
    /// Information about the pay it forward event. Null if notice_type is not pay_it_forward.
    /// </summary>
    public ChatPayItForward? PayItForward { get; set; }
    /// <summary>
    /// Information about the announcement event. Null if notice_type is not announcement
    /// </summary>
    public ChatAnnouncement? Announcement { get; set; }
    /// <summary>
    /// Information about the charity donation event. Null if notice_type is not charity_donation.
    /// </summary>
    public ChatCharityDonation? CharityDonation { get; set; }
    /// <summary>
    /// Information about the bits badge tier event. Null if notice_type is not bits_badge_tier.
    /// </summary>
    public ChannelBitsBadgeTier? BitsBadgeTier { get; set; }
}
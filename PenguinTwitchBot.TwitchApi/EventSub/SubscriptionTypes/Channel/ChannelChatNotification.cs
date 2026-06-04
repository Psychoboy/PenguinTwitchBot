using PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelChatNotification
{
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public string ChatterUserId { get; set; } = string.Empty;
    public string ChatterUserName { get; set; } = string.Empty;
    public string ChatterUserLogin { get; set; } = string.Empty;
    public bool ChatterIsAnonymous { get; set; }
    public string Color { get; set; } = string.Empty;
    public ChatBadge[] Badges { get; set; } = [];
    public string SystemMessage { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
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
    /// <para>shared_chat_sub</para>
    /// <para>shared_chat_resub</para>
    /// <para>shared_chat_sub_gift</para>
    /// <para>shared_chat_community_sub_gift</para>
    /// <para>shared_chat_gift_paid_upgrade</para>
    /// <para>shared_chat_prime_paid_upgrade</para>
    /// <para>shared_chat_raid</para>
    /// <para>shared_chat_pay_it_forward</para>
    /// <para>shared_chat_announcement</para>
    /// </summary>
    public string NoticeType { get; set; } = string.Empty;
    public ChatSub? Sub { get; set; }
    public ChatResub? Resub { get; set; }
    public ChatSubGift? SubGift { get; set; }
    public ChatCommunitySubGift? CommunitySubGift { get; set; }
    public ChatGiftPaidUpgrade? GiftPaidUpgrade { get; set; }
    public ChatPrimePaidUpgrade? PrimePaidUpgrade { get; set; }
    public ChatRaid? Raid { get; set; }
    public ChatUnraid? Unraid { get; set; }
    public ChatPayItForward? PayItForward { get; set; }
    public ChatAnnouncement? Announcement { get; set; }
    public ChatCharityDonation? CharityDonation { get; set; }
    public ChannelBitsBadgeTier? BitsBadgeTier { get; set; }
    public WatchStreak? WatchStreak { get; set; }
    public string? SourceBroadcasterUserId { get; set; }
    public string? SourceBroadcasterUserName { get; set; }
    public string? SourceBroadcasterUserLogin { get; set; }
    public string? SourceMessageId { get; set; }
    public ChatBadge[]? SourceBadges { get; set; }
    public ChatSub? SharedChatSub { get; set; }
    public bool? IsSourceOnly { get; set; }
    public ChatResub? SharedChatResub { get; set; }
    public ChatSubGift? SharedChatSubGift { get; set; }
    public ChatCommunitySubGift? SharedChatCommunitySubGift { get; set; }
    public ChatGiftPaidUpgrade? SharedChatGiftPaidUpgrade { get; set; }
    public ChatPrimePaidUpgrade? SharedChatPrimePaidUpgrade { get; set; }
    public ChatRaid? SharedChatRaid { get; set; }
    public ChatPayItForward? SharedChatPayItForward { get; set; }
    public ChatAnnouncement? SharedChatAnnouncement { get; set; }
}


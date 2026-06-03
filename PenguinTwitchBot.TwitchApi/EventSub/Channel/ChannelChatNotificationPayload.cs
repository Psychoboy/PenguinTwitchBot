namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelChatNotificationPayload : EventSubPayload<ChannelChatNotification>
{
}

public sealed class ChannelChatNotification
{
    public required string BroadcasterUserId { get; set; }
    public required string BroadcasterUserName { get; set; }
    public required string BroadcasterUserLogin { get; set; }
    public required string MessageId { get; set; }
    public required string ChatterUserId { get; set; }
    public required string ChatterUserLogin { get; set; }
    public required string ChatterUserName { get; set; }
    public bool ChatterIsAnonymous { get; set; }
    public string Color { get; set; } = string.Empty;
    public ChatBadge[] Badges { get; set; } = [];
    public string SystemMessage { get; set; } = string.Empty;
    public required string NoticeType { get; set; }
    public ChannelNotificationChatMessage? Message { get; set; }
    public ChatNotificationSubInfo? Sub { get; set; }
    public ChatNotificationResubInfo? Resub { get; set; }
    public ChatNotificationSubGiftInfo? SubGift { get; set; }
    public ChatNotificationCommunitySubGiftInfo? CommunitySubGift { get; set; }
    public ChatNotificationGiftPaidUpgradeInfo? GiftPaidUpgrade { get; set; }
    public ChatNotificationPrimePaidUpgradeInfo? PrimePaidUpgrade { get; set; }
    public ChatNotificationRaidInfo? Raid { get; set; }
    public ChatNotificationPayItForwardInfo? PayItForward { get; set; }
    public ChatNotificationAnnouncementInfo? Announcement { get; set; }
    public ChatNotificationCharityDonationInfo? CharityDonation { get; set; }
    public ChatNotificationBitsBadgeTierInfo? BitsBadgeTier { get; set; }
    public ChatNotificationWatchStreakInfo? WatchStreak { get; set; }
    public string? SourceBroadcasterUserId { get; set; }
    public string? SourceBroadcasterUserName { get; set; }
    public string? SourceBroadcasterUserLogin { get; set; }
    public string? SourceMessageId { get; set; }
    public ChatBadge[]? SourceBadges { get; set; }
    public ChatNotificationSubInfo? SharedChatSub { get; set; }
    public bool? IsSourceOnly { get; set; }
    public ChatNotificationResubInfo? SharedChatResub { get; set; }
    public ChatNotificationSubGiftInfo? SharedChatSubGift { get; set; }
    public ChatNotificationCommunitySubGiftInfo? SharedChatCommunitySubGift { get; set; }
    public ChatNotificationGiftPaidUpgradeInfo? SharedChatGiftPaidUpgrade { get; set; }
    public ChatNotificationPrimePaidUpgradeInfo? SharedChatPrimePaidUpgrade { get; set; }
    public ChatNotificationRaidInfo? SharedChatRaid { get; set; }
    public ChatNotificationPayItForwardInfo? SharedChatPayItForward { get; set; }
    public ChatNotificationAnnouncementInfo? SharedChatAnnouncement { get; set; }
}

public sealed class ChannelNotificationChatMessage
{
    public string Text { get; set; } = string.Empty;
    public ChannelChatMessageFragment[] Fragments { get; set; } = [];
    public ChatBadge[] Badges { get; set; } = [];
    
}

public sealed class ChatNotificationSubInfo
{
    public string SubTier { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public bool IsPrime { get; set; }
}

public sealed class ChatNotificationResubInfo
{
    public int CumulativeMonths { get; set; }
    public int DurationMonths { get; set; }
    public int? StreakMonths { get; set; }
    public string SubTier { get; set; } = string.Empty;
    public bool IsPrime { get; set; }
    public bool IsGift { get; set; }
    public bool? GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
}

public sealed class ChatNotificationSubGiftInfo
{
    public int DurationMonths { get; set; }
    public int? CumulativeTotal { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public string RecipientUserName { get; set; } = string.Empty;
    public string RecipientUserLogin { get; set; } = string.Empty;
    public string SubTier { get; set; } = string.Empty;
    public string? CommunityGiftId { get; set; }
}

public sealed class ChatNotificationCommunitySubGiftInfo
{
    public string Id { get; set; } = string.Empty;
    public int Total { get; set; }
    public string SubTier { get; set; } = string.Empty;
    public int? CumulativeTotal { get; set; }
}

public sealed class ChatNotificationGiftPaidUpgradeInfo
{
    public bool GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
}

public sealed class ChatNotificationPrimePaidUpgradeInfo
{
    public string SubTier { get; set; } = string.Empty;
}

public sealed class ChatNotificationRaidInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public int ViewerCount { get; set; }
    public string ProfileImageUrl { get; set; } = string.Empty;
}

public sealed class ChatNotificationPayItForwardInfo
{
    public bool GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
    public string? RecipientUserId { get; set; }
    public string? RecipientUserName { get; set; }
    public string? RecipientUserLogin { get; set; }
}

public sealed class ChatNotificationAnnouncementInfo
{
    public string Color { get; set; } = string.Empty;
}

public sealed class ChatNotificationCharityDonationInfo
{
    public string CharityName { get; set; } = string.Empty;
    public int AmountValue { get; set; }
    public int AmountDecimalPlaces { get; set; }
    public string AmountCurrency { get; set; } = string.Empty;
}

public sealed class ChatNotificationBitsBadgeTierInfo
{
    public int Tier { get; set; }
}

public sealed class ChatNotificationWatchStreakInfo
{
    public int StreakCount { get; set; }
    public int ChannelPointsAwarded { get; set; }
}

public sealed class ChatBadge
{
    public string SetId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Info { get; set; } = string.Empty;
}

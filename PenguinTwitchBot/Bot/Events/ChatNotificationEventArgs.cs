namespace PenguinTwitchBot.Bot.Events
{
    public class ChatNotificationEventArgs
    {
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public bool IsAnonymous { get; set; }
        public string NoticeType { get; set; } = string.Empty;
        public string SystemMessage { get; set; } = string.Empty;
        public string? Message { get; set; }

        /// <summary>Populated when NoticeType == "sub"</summary>
        public ChatNotificationSubInfo? Sub { get; set; }
        /// <summary>Populated when NoticeType == "resub"</summary>
        public ChatNotificationResubInfo? Resub { get; set; }
        /// <summary>Populated when NoticeType == "sub_gift"</summary>
        public ChatNotificationSubGiftInfo? SubGift { get; set; }
        /// <summary>Populated when NoticeType == "community_sub_gift"</summary>
        public ChatNotificationCommunitySubGiftInfo? CommunitySubGift { get; set; }
        /// <summary>Populated when NoticeType == "gift_paid_upgrade"</summary>
        public ChatNotificationGiftPaidUpgradeInfo? GiftPaidUpgrade { get; set; }
        /// <summary>Populated when NoticeType == "prime_paid_upgrade"</summary>
        public ChatNotificationPrimePaidUpgradeInfo? PrimePaidUpgrade { get; set; }
        /// <summary>Populated when NoticeType == "raid"</summary>
        public ChatNotificationRaidInfo? Raid { get; set; }
        /// <summary>Populated when NoticeType == "pay_it_forward"</summary>
        public ChatNotificationPayItForwardInfo? PayItForward { get; set; }
        /// <summary>Populated when NoticeType == "announcement"</summary>
        public ChatNotificationAnnouncementInfo? Announcement { get; set; }
        /// <summary>Populated when NoticeType == "charity_donation"</summary>
        public ChatNotificationCharityDonationInfo? CharityDonation { get; set; }
        /// <summary>Populated when NoticeType == "bits_badge_tier"</summary>
        public ChatNotificationBitsBadgeTierInfo? BitsBadgeTier { get; set; }
        /// <summary>Populated when NoticeType == "watch_streak"</summary>
        public ChatNotificationWatchStreakInfo? WatchStreak { get; set; }
    }

    public class ChatNotificationSubInfo
    {
        /// <summary>1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3</summary>
        public string SubTier { get; set; } = string.Empty;
        public int DurationMonths { get; set; }
        public bool IsPrime { get; set; }
    }

    public class ChatNotificationResubInfo
    {
        public int CumulativeMonths { get; set; }
        public int DurationMonths { get; set; }
        public int? StreakMonths { get; set; }
        /// <summary>1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3</summary>
        public string SubTier { get; set; } = string.Empty;
        public bool IsPrime { get; set; }
        public bool IsGift { get; set; }
        public bool? GifterIsAnonymous { get; set; }
        public string? GifterUserId { get; set; }
        public string? GifterUserName { get; set; }
        public string? GifterUserLogin { get; set; }
    }

    public class ChatNotificationSubGiftInfo
    {
        public int DurationMonths { get; set; }
        public int? CumulativeTotal { get; set; }
        public string RecipientUserId { get; set; } = string.Empty;
        public string RecipientUserName { get; set; } = string.Empty;
        public string RecipientUserLogin { get; set; } = string.Empty;
        /// <summary>1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3</summary>
        public string SubTier { get; set; } = string.Empty;
        public string? CommunityGiftId { get; set; }
    }

    public class ChatNotificationCommunitySubGiftInfo
    {
        public string Id { get; set; } = string.Empty;
        public int Total { get; set; }
        /// <summary>1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3</summary>
        public string SubTier { get; set; } = string.Empty;
        public int? CumulativeTotal { get; set; }
    }

    public class ChatNotificationGiftPaidUpgradeInfo
    {
        public bool GifterIsAnonymous { get; set; }
        public string? GifterUserId { get; set; }
        public string? GifterUserName { get; set; }
        public string? GifterUserLogin { get; set; }
    }

    public class ChatNotificationPrimePaidUpgradeInfo
    {
        /// <summary>1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3</summary>
        public string SubTier { get; set; } = string.Empty;
    }

    public class ChatNotificationRaidInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public int ViewerCount { get; set; }
        public string ProfileImageUrl { get; set; } = string.Empty;
    }

    public class ChatNotificationPayItForwardInfo
    {
        public bool GifterIsAnonymous { get; set; }
        public string? GifterUserId { get; set; }
        public string? GifterUserName { get; set; }
        public string? GifterUserLogin { get; set; }
        public string? RecipientUserId { get; set; }
        public string? RecipientUserName { get; set; }
        public string? RecipientUserLogin { get; set; }
    }

    public class ChatNotificationAnnouncementInfo
    {
        /// <summary>Announcement highlight color (e.g. "blue", "green", "orange", "purple", "primary")</summary>
        public string Color { get; set; } = string.Empty;
    }

    public class ChatNotificationCharityDonationInfo
    {
        public string CharityName { get; set; } = string.Empty;
        /// <summary>Minor-unit amount (e.g. 550 = $5.50 USD)</summary>
        public int AmountValue { get; set; }
        public int AmountDecimalPlaces { get; set; }
        /// <summary>ISO-4217 currency code (e.g. "USD")</summary>
        public string AmountCurrency { get; set; } = string.Empty;
    }

    public class ChatNotificationBitsBadgeTierInfo
    {
        /// <summary>Tier threshold (e.g. 100, 1000, 10000)</summary>
        public int Tier { get; set; }
    }

    public class ChatNotificationWatchStreakInfo
    {
        public int StreakCount { get; set; }
        public int ChannelPointsAwarded { get; set; }
    }
}

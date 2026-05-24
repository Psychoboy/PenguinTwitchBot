using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Actions.Triggers
{
    /// <summary>
    /// Configuration for Twitch Event triggers with conditional matching
    /// </summary>
    public class TwitchEventTriggerConfig
    {
        /// <summary>
        /// The specific Twitch event name (e.g., "ChannelFollow", "ChannelCheer")
        /// </summary>
        public string EventName { get; set; } = string.Empty;

        /// <summary>
        /// For ChannelCheer - minimum bits amount
        /// </summary>
        public int? MinAmount { get; set; }

        /// <summary>
        /// For ChannelCheer - maximum bits amount
        /// </summary>
        public int? MaxAmount { get; set; }

        /// <summary>
        /// For ChannelRaid - minimum viewer count
        /// </summary>
        public int? MinViewers { get; set; }

        /// <summary>
        /// For ChannelRaid - maximum viewer count
        /// </summary>
        public int? MaxViewers { get; set; }

        /// <summary>
        /// For ChannelPointsCustomRewardRedemptionAdd - specific reward title(s)
        /// Empty or null means ANY reward
        /// </summary>
        public List<string> RewardTitles { get; set; } = new();

        /// <summary>
        /// For ChannelSubscribe/ChannelSubscriptionMessage - specific tier(s)
        /// Empty or null means ANY tier
        /// </summary>
        public List<string> SubscriptionTiers { get; set; } = new();

        /// <summary>
        /// For ChannelSubscriptionGift - minimum gift amount
        /// </summary>
        public int? MinGiftAmount { get; set; }

        /// <summary>
        /// For ChannelSubscriptionGift - maximum gift amount
        /// </summary>
        public int? MaxGiftAmount { get; set; }

        /// <summary>
        /// For ChannelAdBreakBegin - filter by automatic vs manual
        /// null means ANY type
        /// </summary>
        public bool? IsAutomatic { get; set; }

        /// <summary>
        /// For ChannelAdBreakBegin - minimum ad duration in seconds
        /// </summary>
        public int? MinAdDuration { get; set; }

        /// <summary>
        /// For ChannelAdBreakBegin - maximum ad duration in seconds
        /// </summary>
        public int? MaxAdDuration { get; set; }

        /// <summary>
        /// For ChannelBitsUse - filter by bits use type(s): cheer, power_up, custom_power_up.
        /// Empty or null means ANY type.
        /// </summary>
        public List<string> BitsTypes { get; set; } = new();

        /// <summary>
        /// For ChannelBitsUse (power_up) - filter by standard Power-Up type(s):
        /// message_effect, celebration, gigantify_an_emote.
        /// Empty or null means ANY power-up type.
        /// </summary>
        public List<string> PowerUpTypes { get; set; } = new();

        /// <summary>
        /// For ChannelBitsUse (custom_power_up) - filter by specific custom Power-Up title(s).
        /// Empty or null means ANY custom power-up title.
        /// </summary>
        public List<string> CustomPowerUpTitles { get; set; } = new();

        /// <summary>
        /// For ChannelBitsUse (custom_power_up) - filter by specific custom Power-Up reward ID(s).
        /// Empty or null means ANY custom power-up reward.
        /// </summary>
        public List<string> CustomPowerUpRewardIds { get; set; } = new();

        /// <summary>
        /// For ChannelChatNotification - filter by specific notice type(s):
        /// sub, resub, sub_gift, community_sub_gift, gift_paid_upgrade, prime_paid_upgrade,
        /// raid, unraid, pay_it_forward, announcement, bits_badge_tier, charity_donation, watch_streak.
        /// At least one notice type must be specified for ChannelChatNotification — an empty list means the trigger will never fire.
        /// </summary>
        public List<string> NoticeTypes { get; set; } = new();

        /// <summary>
        /// For ChannelChatNotification (watch_streak) - minimum consecutive streams watched.
        /// </summary>
        public int? MinWatchStreak { get; set; }

        /// <summary>
        /// For ChannelChatNotification (watch_streak) - maximum consecutive streams watched.
        /// </summary>
        public int? MaxWatchStreak { get; set; }

        /// <summary>
        /// For ChannelChatNotification (raid) - minimum viewer count in the raid.
        /// </summary>
        public int? MinRaidViewers { get; set; }

        /// <summary>
        /// For ChannelChatNotification (raid) - maximum viewer count in the raid.
        /// </summary>
        public int? MaxRaidViewers { get; set; }

        /// <summary>
        /// For ChannelChatNotification (sub/resub/sub_gift/community_sub_gift/prime_paid_upgrade) -
        /// filter by sub tier. Values: "1000" (T1), "2000" (T2), "3000" (T3). Empty = any tier.
        /// </summary>
        public List<string> SubTiers { get; set; } = new();

        /// <summary>
        /// For ChannelChatNotification (resub) - minimum cumulative months subscribed.
        /// </summary>
        public int? MinCumulativeMonths { get; set; }

        /// <summary>
        /// For ChannelChatNotification (resub) - maximum cumulative months subscribed.
        /// </summary>
        public int? MaxCumulativeMonths { get; set; }

        /// <summary>
        /// For ChannelChatNotification (community_sub_gift) - minimum number of subs gifted.
        /// </summary>
        public int? MinCommunityGiftCount { get; set; }

        /// <summary>
        /// For ChannelChatNotification (community_sub_gift) - maximum number of subs gifted.
        /// </summary>
        public int? MaxCommunityGiftCount { get; set; }

        /// <summary>
        /// For ChannelChatNotification (charity_donation) - minimum donation amount in minor units (e.g. 500 = $5.00).
        /// </summary>
        public int? MinCharityAmount { get; set; }

        /// <summary>
        /// For ChannelChatNotification (charity_donation) - maximum donation amount in minor units.
        /// </summary>
        public int? MaxCharityAmount { get; set; }

        /// <summary>
        /// For ChannelChatNotification (bits_badge_tier) - minimum tier threshold (e.g. 100, 1000, 10000).
        /// </summary>
        public int? MinBitsBadgeTier { get; set; }

        /// <summary>
        /// For ChannelChatNotification (bits_badge_tier) - maximum tier threshold.
        /// </summary>
        public int? MaxBitsBadgeTier { get; set; }

        /// <summary>
        /// For ChannelChatNotification (announcement) - filter by color.
        /// Values: "blue", "green", "orange", "purple", "primary". Empty = any color.
        /// </summary>
        public List<string> AnnouncementColors { get; set; } = new();

        /// <summary>
        /// Deserialize from JSON string
        /// </summary>
        public static TwitchEventTriggerConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new TwitchEventTriggerConfig();
            }

            try
            {
                return JsonSerializer.Deserialize<TwitchEventTriggerConfig>(json) ?? new TwitchEventTriggerConfig();
            }
            catch
            {
                return new TwitchEventTriggerConfig();
            }
        }

        /// <summary>
        /// Try to deserialize from JSON string
        /// </summary>
        /// <returns>True if deserialization succeeded, false if JSON is invalid</returns>
        public static bool TryFromJson(string json, out TwitchEventTriggerConfig? config)
        {
            config = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                config = new TwitchEventTriggerConfig();
                return true;
            }

            try
            {
                config = JsonSerializer.Deserialize<TwitchEventTriggerConfig>(json);
                return config != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Serialize to JSON string
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Check if this configuration matches the given event data
        /// </summary>
        public bool Matches(ConcurrentDictionary<string, string> eventVariables)
        {
            // Check cheer amount range
            if (MinAmount.HasValue || MaxAmount.HasValue)
            {
                if (!eventVariables.TryGetValue("Amount", out var amountStr) || !int.TryParse(amountStr, out var amount))
                {
                    return false;
                }

                if (MinAmount.HasValue && amount < MinAmount.Value)
                {
                    return false;
                }

                if (MaxAmount.HasValue && amount > MaxAmount.Value)
                {
                    return false;
                }
            }

            // Check viewer count range (for raids)
            if (MinViewers.HasValue || MaxViewers.HasValue)
            {
                if (!eventVariables.TryGetValue("Viewers", out var viewersStr) && !eventVariables.TryGetValue("NumberOfViewers", out viewersStr))
                {
                    return false;
                }

                if (!int.TryParse(viewersStr, out var viewers))
                {
                    return false;
                }

                if (MinViewers.HasValue && viewers < MinViewers.Value)
                {
                    return false;
                }

                if (MaxViewers.HasValue && viewers > MaxViewers.Value)
                {
                    return false;
                }
            }

            // Check reward titles (for channel points)
            if (RewardTitles.Count > 0)
            {
                if (!eventVariables.TryGetValue("Title", out var title) && !eventVariables.TryGetValue("RewardName", out title))
                {
                    return false;
                }

                if (!RewardTitles.Any(rt => rt.Equals(title, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check subscription tiers
            if (SubscriptionTiers.Count > 0)
            {
                if (!eventVariables.TryGetValue("Tier", out var tier))
                {
                    return false;
                }

                if (!SubscriptionTiers.Any(st => st.Equals(tier, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check gift amount range
            if (MinGiftAmount.HasValue || MaxGiftAmount.HasValue)
            {
                if (!eventVariables.TryGetValue("GiftAmount", out var giftAmountStr) || !int.TryParse(giftAmountStr, out var giftAmount))
                {
                    return false;
                }

                if (MinGiftAmount.HasValue && giftAmount < MinGiftAmount.Value)
                {
                    return false;
                }

                if (MaxGiftAmount.HasValue && giftAmount > MaxGiftAmount.Value)
                {
                    return false;
                }
            }

            // Check ad break automatic/manual
            if (IsAutomatic.HasValue)
            {
                if (!eventVariables.TryGetValue("Automatic", out var automaticStr) || !bool.TryParse(automaticStr, out var automatic))
                {
                    return false;
                }

                if (automatic != IsAutomatic.Value)
                {
                    return false;
                }
            }

            // Check ad duration range
            if (MinAdDuration.HasValue || MaxAdDuration.HasValue)
            {
                if (!eventVariables.TryGetValue("Length", out var lengthStr) || !int.TryParse(lengthStr, out var length))
                {
                    return false;
                }

                if (MinAdDuration.HasValue && length < MinAdDuration.Value)
                {
                    return false;
                }

                if (MaxAdDuration.HasValue && length > MaxAdDuration.Value)
                {
                    return false;
                }
            }

            // Check bits use type (for channel bits use)
            if (BitsTypes.Count > 0)
            {
                if (!eventVariables.TryGetValue("Type", out var bitsType))
                {
                    return false;
                }

                if (!BitsTypes.Any(bt => bt.Equals(bitsType, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check standard Power-Up type (message_effect, celebration, gigantify_an_emote)
            if (PowerUpTypes.Count > 0)
            {
                if (!eventVariables.TryGetValue("PowerUpType", out var powerUpType) ||
                    string.IsNullOrEmpty(powerUpType))
                {
                    return false;
                }

                if (!PowerUpTypes.Any(pt => pt.Equals(powerUpType, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check custom Power-Up title
            if (CustomPowerUpTitles.Count > 0)
            {
                if (!eventVariables.TryGetValue("CustomPowerUpTitle", out var customTitle) ||
                    string.IsNullOrEmpty(customTitle))
                {
                    return false;
                }

                if (!CustomPowerUpTitles.Any(t => t.Equals(customTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check custom Power-Up reward ID
            if (CustomPowerUpRewardIds.Count > 0)
            {
                if (!eventVariables.TryGetValue("CustomPowerUpRewardId", out var rewardId) ||
                    string.IsNullOrEmpty(rewardId))
                {
                    return false;
                }

                if (!CustomPowerUpRewardIds.Any(r => r.Equals(rewardId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check notice type (for ChannelChatNotification)
            // Require at least one notice type to be configured — without one the trigger would fire for every notice.
            if (EventName.Equals("ChannelChatNotification", StringComparison.OrdinalIgnoreCase) &&
                NoticeTypes.Count == 0)
            {
                return false;
            }

            if (NoticeTypes.Count > 0)
            {
                if (!eventVariables.TryGetValue("NoticeType", out var noticeType) ||
                    string.IsNullOrEmpty(noticeType))
                {
                    return false;
                }

                if (!NoticeTypes.Any(nt => nt.Equals(noticeType, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check watch streak range (for ChannelChatNotification watch_streak)
            if (MinWatchStreak.HasValue || MaxWatchStreak.HasValue)
            {
                if (!eventVariables.TryGetValue("WatchStreak.StreakCount", out var streakStr) ||
                    !int.TryParse(streakStr, out var streakCount))
                {
                    return false;
                }

                if (MinWatchStreak.HasValue && streakCount < MinWatchStreak.Value)
                {
                    return false;
                }

                if (MaxWatchStreak.HasValue && streakCount > MaxWatchStreak.Value)
                {
                    return false;
                }
            }

            // Check raid viewer count range (for ChannelChatNotification raid)
            if (MinRaidViewers.HasValue || MaxRaidViewers.HasValue)
            {
                if (!eventVariables.TryGetValue("Raid.ViewerCount", out var viewerStr) ||
                    !int.TryParse(viewerStr, out var viewerCount))
                {
                    return false;
                }

                if (MinRaidViewers.HasValue && viewerCount < MinRaidViewers.Value)
                {
                    return false;
                }

                if (MaxRaidViewers.HasValue && viewerCount > MaxRaidViewers.Value)
                {
                    return false;
                }
            }

            // Check sub tier filter (for sub/resub/sub_gift/community_sub_gift/prime_paid_upgrade)
            if (SubTiers.Count > 0)
            {
                var noticeType = (eventVariables.TryGetValue("NoticeType", out var nt) ? nt : string.Empty).ToLowerInvariant();
                var subTierKey = noticeType switch
                {
                    "sub" => "Sub.SubTier",
                    "resub" => "Resub.SubTier",
                    "sub_gift" => "SubGift.SubTier",
                    "community_sub_gift" => "CommunitySubGift.SubTier",
                    "prime_paid_upgrade" => "PrimePaidUpgrade.SubTier",
                    _ => null
                };

                if (subTierKey == null ||
                    !eventVariables.TryGetValue(subTierKey, out var subTier) ||
                    string.IsNullOrEmpty(subTier) ||
                    !SubTiers.Any(t => t.Equals(subTier, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check cumulative months range (for resub)
            if (MinCumulativeMonths.HasValue || MaxCumulativeMonths.HasValue)
            {
                if (!eventVariables.TryGetValue("Resub.CumulativeMonths", out var monthsStr) ||
                    !int.TryParse(monthsStr, out var months))
                {
                    return false;
                }

                if (MinCumulativeMonths.HasValue && months < MinCumulativeMonths.Value)
                    return false;

                if (MaxCumulativeMonths.HasValue && months > MaxCumulativeMonths.Value)
                    return false;
            }

            // Check community gift count range (for community_sub_gift)
            if (MinCommunityGiftCount.HasValue || MaxCommunityGiftCount.HasValue)
            {
                if (!eventVariables.TryGetValue("CommunitySubGift.Total", out var totalStr) ||
                    !int.TryParse(totalStr, out var total))
                {
                    return false;
                }

                if (MinCommunityGiftCount.HasValue && total < MinCommunityGiftCount.Value)
                    return false;

                if (MaxCommunityGiftCount.HasValue && total > MaxCommunityGiftCount.Value)
                    return false;
            }

            // Check charity donation amount range (for charity_donation, amount in minor units)
            if (MinCharityAmount.HasValue || MaxCharityAmount.HasValue)
            {
                if (!eventVariables.TryGetValue("CharityDonation.AmountValue", out var amountStr) ||
                    !int.TryParse(amountStr, out var amount))
                {
                    return false;
                }

                if (MinCharityAmount.HasValue && amount < MinCharityAmount.Value)
                    return false;

                if (MaxCharityAmount.HasValue && amount > MaxCharityAmount.Value)
                    return false;
            }

            // Check bits badge tier range (for bits_badge_tier)
            if (MinBitsBadgeTier.HasValue || MaxBitsBadgeTier.HasValue)
            {
                if (!eventVariables.TryGetValue("BitsBadgeTier.Tier", out var tierStr) ||
                    !int.TryParse(tierStr, out var tier))
                {
                    return false;
                }

                if (MinBitsBadgeTier.HasValue && tier < MinBitsBadgeTier.Value)
                    return false;

                if (MaxBitsBadgeTier.HasValue && tier > MaxBitsBadgeTier.Value)
                    return false;
            }

            // Check announcement color filter (for announcement)
            if (AnnouncementColors.Count > 0)
            {
                if (!eventVariables.TryGetValue("Announcement.Color", out var color) ||
                    string.IsNullOrEmpty(color) ||
                    !AnnouncementColors.Any(c => c.Equals(color, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

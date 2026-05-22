using PenguinTwitchBot.Bot.Events;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Actions.Utilities
{
    /// <summary>
    /// Utility class for converting various Twitch event args to Dictionary for variable substitution
    /// </summary>
    public static class TwitchEventArgsConverter
    {
        public static ConcurrentDictionary<string, string> ToDictionary(FollowEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Username"] = eventArgs.Username ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["FollowDate"] = eventArgs.FollowDate.ToString("o"),
                ["FollowEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(CheerEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["Message"] = eventArgs.Message ?? string.Empty,
                ["Amount"] = eventArgs.Amount.ToString(),
                ["IsAnonymous"] = eventArgs.IsAnonymous.ToString(),
                ["CheerEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(SubscriptionEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
    
            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["Count"] = eventArgs.Count?.ToString() ?? string.Empty,
                ["Streak"] = eventArgs.Streak?.ToString() ?? string.Empty,
                ["Tier"] = eventArgs.Tier ?? string.Empty,
                ["IsGift"] = eventArgs.IsGift.ToString(),
                ["IsRenewal"] = eventArgs.IsRenewal.ToString(),
                ["HadPreviousSub"] = eventArgs.HadPreviousSub.ToString(),
                ["Message"] = eventArgs.Message ?? string.Empty,
                ["SubscriptionEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(SubscriptionGiftEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["GiftAmount"] = eventArgs.GiftAmount.ToString(),
                ["TotalGifted"] = eventArgs.TotalGifted?.ToString() ?? string.Empty,
                ["SubscriptionGiftEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(SubscriptionEndEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["User"] = eventArgs.Name ?? string.Empty,
                ["SubscriptionEndEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(RaidEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["NumberOfViewers"] = eventArgs.NumberOfViewers.ToString(),
                ["Viewers"] = eventArgs.NumberOfViewers.ToString(),
                ["RaidEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(ChannelPointRedeemEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Sender"] = eventArgs.Sender ?? string.Empty,
                ["Name"] = eventArgs.Sender ?? string.Empty,
                ["Username"] = eventArgs.Username ?? eventArgs.Sender ?? string.Empty,
                ["DisplayName"] = eventArgs.Sender ?? string.Empty,
                ["User"] = eventArgs.Sender ?? string.Empty,
                ["Title"] = eventArgs.Title ?? string.Empty,
                ["RewardName"] = eventArgs.Title ?? string.Empty,
                ["UserInput"] = eventArgs.UserInput ?? string.Empty,
                ["Message"] = eventArgs.UserInput ?? string.Empty,
                ["ChannelPointRedeemEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(AdBreakStartEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Length"] = eventArgs.Length.ToString(),
                ["Automatic"] = eventArgs.Automatic.ToString(),
                ["StartedAt"] = eventArgs.StartedAt.ToString("o"),
                ["AdBreakStartEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(BanEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["User"] = eventArgs.Name ?? string.Empty,
                ["IsUnBan"] = eventArgs.IsUnBan.ToString(),
                ["BanEndsAt"] = eventArgs.BanEndsAt?.ToString("o") ?? string.Empty,
                ["BanEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(BitsUseEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["Amount"] = eventArgs.Amount.ToString(),
                ["Bits"] = eventArgs.Amount.ToString(),
                ["Message"] = eventArgs.Message ?? string.Empty,
                ["Type"] = eventArgs.Type,
                ["IsPowerUp"] = eventArgs.IsPowerUp.ToString(),
                ["PowerUpType"] = eventArgs.PowerUp?.Type ?? string.Empty,
                ["IsCustomPowerUp"] = eventArgs.IsCustomPowerUp.ToString(),
                ["CustomPowerUpTitle"] = eventArgs.CustomPowerUp?.Title ?? string.Empty,
                ["CustomPowerUpRewardId"] = eventArgs.CustomPowerUp?.RewardId ?? string.Empty,
                ["HasBitsMessage"] = eventArgs.HasBitsMessage.ToString(),
                ["BitsUseEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(ChatNotificationEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Common fields
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["User"] = eventArgs.DisplayName ?? string.Empty,
                ["IsAnonymous"] = eventArgs.IsAnonymous.ToString(),
                ["NoticeType"] = eventArgs.NoticeType,
                ["SystemMessage"] = eventArgs.SystemMessage,
                ["Message"] = eventArgs.Message ?? string.Empty,
                // Sub (notice_type == "sub")
                ["Sub.SubTier"] = eventArgs.Sub?.SubTier ?? string.Empty,
                ["Sub.DurationMonths"] = eventArgs.Sub?.DurationMonths.ToString() ?? string.Empty,
                ["Sub.IsPrime"] = eventArgs.Sub?.IsPrime.ToString() ?? string.Empty,
                // Resub (notice_type == "resub")
                ["Resub.CumulativeMonths"] = eventArgs.Resub?.CumulativeMonths.ToString() ?? string.Empty,
                ["Resub.DurationMonths"] = eventArgs.Resub?.DurationMonths.ToString() ?? string.Empty,
                ["Resub.StreakMonths"] = eventArgs.Resub?.StreakMonths?.ToString() ?? string.Empty,
                ["Resub.SubTier"] = eventArgs.Resub?.SubTier ?? string.Empty,
                ["Resub.IsPrime"] = eventArgs.Resub?.IsPrime.ToString() ?? string.Empty,
                ["Resub.IsGift"] = eventArgs.Resub?.IsGift.ToString() ?? string.Empty,
                ["Resub.GifterIsAnonymous"] = eventArgs.Resub?.GifterIsAnonymous?.ToString() ?? string.Empty,
                ["Resub.GifterUserId"] = eventArgs.Resub?.GifterUserId ?? string.Empty,
                ["Resub.GifterUserName"] = eventArgs.Resub?.GifterUserName ?? string.Empty,
                ["Resub.GifterUserLogin"] = eventArgs.Resub?.GifterUserLogin ?? string.Empty,
                // SubGift (notice_type == "sub_gift")
                ["SubGift.DurationMonths"] = eventArgs.SubGift?.DurationMonths.ToString() ?? string.Empty,
                ["SubGift.CumulativeTotal"] = eventArgs.SubGift?.CumulativeTotal?.ToString() ?? string.Empty,
                ["SubGift.RecipientUserId"] = eventArgs.SubGift?.RecipientUserId ?? string.Empty,
                ["SubGift.RecipientUserName"] = eventArgs.SubGift?.RecipientUserName ?? string.Empty,
                ["SubGift.RecipientUserLogin"] = eventArgs.SubGift?.RecipientUserLogin ?? string.Empty,
                ["SubGift.SubTier"] = eventArgs.SubGift?.SubTier ?? string.Empty,
                ["SubGift.CommunityGiftId"] = eventArgs.SubGift?.CommunityGiftId ?? string.Empty,
                // CommunitySubGift (notice_type == "community_sub_gift")
                ["CommunitySubGift.Id"] = eventArgs.CommunitySubGift?.Id ?? string.Empty,
                ["CommunitySubGift.Total"] = eventArgs.CommunitySubGift?.Total.ToString() ?? string.Empty,
                ["CommunitySubGift.SubTier"] = eventArgs.CommunitySubGift?.SubTier ?? string.Empty,
                ["CommunitySubGift.CumulativeTotal"] = eventArgs.CommunitySubGift?.CumulativeTotal?.ToString() ?? string.Empty,
                // GiftPaidUpgrade (notice_type == "gift_paid_upgrade")
                ["GiftPaidUpgrade.GifterIsAnonymous"] = eventArgs.GiftPaidUpgrade?.GifterIsAnonymous.ToString() ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserId"] = eventArgs.GiftPaidUpgrade?.GifterUserId ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserName"] = eventArgs.GiftPaidUpgrade?.GifterUserName ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserLogin"] = eventArgs.GiftPaidUpgrade?.GifterUserLogin ?? string.Empty,
                // PrimePaidUpgrade (notice_type == "prime_paid_upgrade")
                ["PrimePaidUpgrade.SubTier"] = eventArgs.PrimePaidUpgrade?.SubTier ?? string.Empty,
                // Raid (notice_type == "raid")
                ["Raid.UserId"] = eventArgs.Raid?.UserId ?? string.Empty,
                ["Raid.UserName"] = eventArgs.Raid?.UserName ?? string.Empty,
                ["Raid.UserLogin"] = eventArgs.Raid?.UserLogin ?? string.Empty,
                ["Raid.ViewerCount"] = eventArgs.Raid?.ViewerCount.ToString() ?? string.Empty,
                ["Raid.ProfileImageUrl"] = eventArgs.Raid?.ProfileImageUrl ?? string.Empty,
                // PayItForward (notice_type == "pay_it_forward")
                ["PayItForward.GifterIsAnonymous"] = eventArgs.PayItForward?.GifterIsAnonymous.ToString() ?? string.Empty,
                ["PayItForward.GifterUserId"] = eventArgs.PayItForward?.GifterUserId ?? string.Empty,
                ["PayItForward.GifterUserName"] = eventArgs.PayItForward?.GifterUserName ?? string.Empty,
                ["PayItForward.GifterUserLogin"] = eventArgs.PayItForward?.GifterUserLogin ?? string.Empty,
                ["PayItForward.RecipientUserId"] = eventArgs.PayItForward?.RecipientUserId ?? string.Empty,
                ["PayItForward.RecipientUserName"] = eventArgs.PayItForward?.RecipientUserName ?? string.Empty,
                ["PayItForward.RecipientUserLogin"] = eventArgs.PayItForward?.RecipientUserLogin ?? string.Empty,
                // Announcement (notice_type == "announcement")
                ["Announcement.Color"] = eventArgs.Announcement?.Color ?? string.Empty,
                // CharityDonation (notice_type == "charity_donation")
                ["CharityDonation.CharityName"] = eventArgs.CharityDonation?.CharityName ?? string.Empty,
                ["CharityDonation.AmountValue"] = eventArgs.CharityDonation?.AmountValue.ToString() ?? string.Empty,
                ["CharityDonation.AmountDecimalPlaces"] = eventArgs.CharityDonation?.AmountDecimalPlaces.ToString() ?? string.Empty,
                ["CharityDonation.AmountCurrency"] = eventArgs.CharityDonation?.AmountCurrency ?? string.Empty,
                // BitsBadgeTier (notice_type == "bits_badge_tier")
                ["BitsBadgeTier.Tier"] = eventArgs.BitsBadgeTier?.Tier.ToString() ?? string.Empty,
                // WatchStreak (notice_type == "watch_streak")
                ["WatchStreak.StreakCount"] = eventArgs.WatchStreak?.StreakCount.ToString() ?? string.Empty,
                ["WatchStreak.ChannelPointsAwarded"] = eventArgs.WatchStreak?.ChannelPointsAwarded.ToString() ?? string.Empty,
                ["ChatNotificationEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> StreamOnlineVariables()
        {
            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EventType"] = "StreamOnline",
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> StreamOfflineVariables()
        {
            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EventType"] = "StreamOffline",
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            return dictionary;
        }
    }
}

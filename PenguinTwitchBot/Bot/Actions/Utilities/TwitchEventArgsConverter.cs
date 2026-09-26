using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Helpers;
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
                ["Username"] = ViewerInputSanitizer.Sanitize(eventArgs.Username),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
                ["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["Count"] = eventArgs.Count?.ToString() ?? string.Empty,
                ["Streak"] = eventArgs.Streak?.ToString() ?? string.Empty,
                ["Tier"] = eventArgs.Tier ?? string.Empty,
                ["IsGift"] = eventArgs.IsGift.ToString(),
                ["IsRenewal"] = eventArgs.IsRenewal.ToString(),
                ["HadPreviousSub"] = eventArgs.HadPreviousSub.ToString(),
                ["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
                ["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
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
                ["Sender"] = ViewerInputSanitizer.Sanitize(eventArgs.Sender),
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Sender),
                ["Username"] = ViewerInputSanitizer.Sanitize(eventArgs.Username ?? eventArgs.Sender),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.Sender),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.Sender),
                ["Title"] = eventArgs.Title ?? string.Empty,
                ["RewardName"] = eventArgs.Title ?? string.Empty,
                ["UserInput"] = ViewerInputSanitizer.Sanitize(eventArgs.UserInput),
                ["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.UserInput),
                ["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.UserInput),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["Amount"] = eventArgs.Amount.ToString(),
                ["Bits"] = eventArgs.Amount.ToString(),
                ["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
                ["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
                ["Type"] = eventArgs.Type,
                ["IsPowerUp"] = eventArgs.IsPowerUp.ToString(),
                ["PowerUpType"] = eventArgs.PowerUp?.Type ?? string.Empty,
                ["IsCustomPowerUp"] = eventArgs.IsCustomPowerUp.ToString(),
                ["CustomPowerUpTitle"] = ViewerInputSanitizer.Sanitize(eventArgs.CustomPowerUp?.Title),
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
                ["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name),
                ["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName),
                ["IsAnonymous"] = eventArgs.IsAnonymous.ToString(),
                ["NoticeType"] = eventArgs.NoticeType,
                ["SystemMessage"] = ViewerInputSanitizer.Sanitize(eventArgs.SystemMessage),
                ["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
                ["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Message),
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
                ["Resub.GifterUserName"] = ViewerInputSanitizer.Sanitize(eventArgs.Resub?.GifterUserName),
                ["Resub.GifterUserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.Resub?.GifterUserLogin),
                // SubGift (notice_type == "sub_gift")
                ["SubGift.DurationMonths"] = eventArgs.SubGift?.DurationMonths.ToString() ?? string.Empty,
                ["SubGift.CumulativeTotal"] = eventArgs.SubGift?.CumulativeTotal?.ToString() ?? string.Empty,
                ["SubGift.RecipientUserId"] = eventArgs.SubGift?.RecipientUserId ?? string.Empty,
                ["SubGift.RecipientUserName"] = ViewerInputSanitizer.Sanitize(eventArgs.SubGift?.RecipientUserName),
                ["SubGift.RecipientUserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.SubGift?.RecipientUserLogin),
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
                ["GiftPaidUpgrade.GifterUserName"] = ViewerInputSanitizer.Sanitize(eventArgs.GiftPaidUpgrade?.GifterUserName),
                ["GiftPaidUpgrade.GifterUserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.GiftPaidUpgrade?.GifterUserLogin),
                // PrimePaidUpgrade (notice_type == "prime_paid_upgrade")
                ["PrimePaidUpgrade.SubTier"] = eventArgs.PrimePaidUpgrade?.SubTier ?? string.Empty,
                // Raid (notice_type == "raid")
                ["Raid.UserId"] = eventArgs.Raid?.UserId ?? string.Empty,
                ["Raid.UserName"] = ViewerInputSanitizer.Sanitize(eventArgs.Raid?.UserName),
                ["Raid.UserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.Raid?.UserLogin),
                ["Raid.ViewerCount"] = eventArgs.Raid?.ViewerCount.ToString() ?? string.Empty,
                ["Raid.ProfileImageUrl"] = eventArgs.Raid?.ProfileImageUrl ?? string.Empty,
                // PayItForward (notice_type == "pay_it_forward")
                ["PayItForward.GifterIsAnonymous"] = eventArgs.PayItForward?.GifterIsAnonymous.ToString() ?? string.Empty,
                ["PayItForward.GifterUserId"] = eventArgs.PayItForward?.GifterUserId ?? string.Empty,
                ["PayItForward.GifterUserName"] = ViewerInputSanitizer.Sanitize(eventArgs.PayItForward?.GifterUserName),
                ["PayItForward.GifterUserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.PayItForward?.GifterUserLogin),
                ["PayItForward.RecipientUserId"] = eventArgs.PayItForward?.RecipientUserId ?? string.Empty,
                ["PayItForward.RecipientUserName"] = ViewerInputSanitizer.Sanitize(eventArgs.PayItForward?.RecipientUserName),
                ["PayItForward.RecipientUserLogin"] = ViewerInputSanitizer.Sanitize(eventArgs.PayItForward?.RecipientUserLogin),
                // Announcement (notice_type == "announcement")
                ["Announcement.Color"] = eventArgs.Announcement?.Color ?? string.Empty,
                // CharityDonation (notice_type == "charity_donation")
                ["CharityDonation.CharityName"] = ViewerInputSanitizer.Sanitize(eventArgs.CharityDonation?.CharityName),
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
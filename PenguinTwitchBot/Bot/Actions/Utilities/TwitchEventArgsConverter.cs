using System.Collections.Concurrent;
using System.Text.Json;
using EventSubChannel = PenguinTwitchBot.TwitchApi.EventSub.Channel;

namespace PenguinTwitchBot.Bot.Actions.Utilities
{
    /// <summary>
    /// Utility class for converting various Twitch event args to Dictionary for variable substitution
    /// </summary>
    public static class TwitchEventArgsConverter
    {
        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelChatNotification eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.ChatterUserId ?? string.Empty,
                ["Name"] = eventArgs.ChatterUserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.ChatterUserName ?? string.Empty,
                ["User"] = eventArgs.ChatterUserName ?? string.Empty,
                ["IsAnonymous"] = eventArgs.ChatterIsAnonymous.ToString(),
                ["NoticeType"] = eventArgs.NoticeType ?? string.Empty,
                ["SystemMessage"] = eventArgs.SystemMessage ?? string.Empty,
                ["Message"] = eventArgs.Message ?? string.Empty,
                ["Sub.SubTier"] = eventArgs.Sub?.SubTier ?? string.Empty,
                ["Sub.DurationMonths"] = eventArgs.Sub?.DurationMonths.ToString() ?? string.Empty,
                ["Sub.IsPrime"] = eventArgs.Sub?.IsPrime.ToString() ?? string.Empty,
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
                ["SubGift.DurationMonths"] = eventArgs.SubGift?.DurationMonths.ToString() ?? string.Empty,
                ["SubGift.CumulativeTotal"] = eventArgs.SubGift?.CumulativeTotal?.ToString() ?? string.Empty,
                ["SubGift.RecipientUserId"] = eventArgs.SubGift?.RecipientUserId ?? string.Empty,
                ["SubGift.RecipientUserName"] = eventArgs.SubGift?.RecipientUserName ?? string.Empty,
                ["SubGift.RecipientUserLogin"] = eventArgs.SubGift?.RecipientUserLogin ?? string.Empty,
                ["SubGift.SubTier"] = eventArgs.SubGift?.SubTier ?? string.Empty,
                ["SubGift.CommunityGiftId"] = eventArgs.SubGift?.CommunityGiftId ?? string.Empty,
                ["CommunitySubGift.Id"] = eventArgs.CommunitySubGift?.Id ?? string.Empty,
                ["CommunitySubGift.Total"] = eventArgs.CommunitySubGift?.Total.ToString() ?? string.Empty,
                ["CommunitySubGift.SubTier"] = eventArgs.CommunitySubGift?.SubTier ?? string.Empty,
                ["CommunitySubGift.CumulativeTotal"] = eventArgs.CommunitySubGift?.CumulativeTotal?.ToString() ?? string.Empty,
                ["GiftPaidUpgrade.GifterIsAnonymous"] = eventArgs.GiftPaidUpgrade?.GifterIsAnonymous.ToString() ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserId"] = eventArgs.GiftPaidUpgrade?.GifterUserId ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserName"] = eventArgs.GiftPaidUpgrade?.GifterUserName ?? string.Empty,
                ["GiftPaidUpgrade.GifterUserLogin"] = eventArgs.GiftPaidUpgrade?.GifterUserLogin ?? string.Empty,
                ["PrimePaidUpgrade.SubTier"] = eventArgs.PrimePaidUpgrade?.SubTier ?? string.Empty,
                ["Raid.UserId"] = eventArgs.Raid?.UserId ?? string.Empty,
                ["Raid.UserName"] = eventArgs.Raid?.UserName ?? string.Empty,
                ["Raid.UserLogin"] = eventArgs.Raid?.UserLogin ?? string.Empty,
                ["Raid.ViewerCount"] = eventArgs.Raid?.ViewerCount.ToString() ?? string.Empty,
                ["Raid.ProfileImageUrl"] = eventArgs.Raid?.ProfileImageUrl ?? string.Empty,
                ["PayItForward.GifterIsAnonymous"] = eventArgs.PayItForward?.GifterIsAnonymous.ToString() ?? string.Empty,
                ["PayItForward.GifterUserId"] = eventArgs.PayItForward?.GifterUserId ?? string.Empty,
                ["PayItForward.GifterUserName"] = eventArgs.PayItForward?.GifterUserName ?? string.Empty,
                ["PayItForward.GifterUserLogin"] = eventArgs.PayItForward?.GifterUserLogin ?? string.Empty,
                ["PayItForward.RecipientUserId"] = eventArgs.PayItForward?.RecipientUserId ?? string.Empty,
                ["PayItForward.RecipientUserName"] = eventArgs.PayItForward?.RecipientUserName ?? string.Empty,
                ["PayItForward.RecipientUserLogin"] = eventArgs.PayItForward?.RecipientUserLogin ?? string.Empty,
                ["Announcement.Color"] = eventArgs.Announcement?.Color ?? string.Empty,
                ["CharityDonation.CharityName"] = eventArgs.CharityDonation?.CharityName ?? string.Empty,
                ["CharityDonation.AmountValue"] = eventArgs.CharityDonation?.AmountValue.ToString() ?? string.Empty,
                ["CharityDonation.AmountDecimalPlaces"] = eventArgs.CharityDonation?.AmountDecimalPlaces.ToString() ?? string.Empty,
                ["CharityDonation.AmountCurrency"] = eventArgs.CharityDonation?.AmountCurrency ?? string.Empty,
                ["BitsBadgeTier.Tier"] = eventArgs.BitsBadgeTier?.Tier.ToString() ?? string.Empty,
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

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelRaid eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.FromBroadcasterUserId ?? string.Empty,
                ["Name"] = eventArgs.FromBroadcasterUserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.FromBroadcasterUserName ?? string.Empty,
                ["User"] = eventArgs.FromBroadcasterUserName ?? string.Empty,
                ["NumberOfViewers"] = eventArgs.Viewers.ToString(),
                ["Viewers"] = eventArgs.Viewers.ToString(),
                ["RaidEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelFollow eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Username"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["FollowDate"] = eventArgs.FollowedAt.ToString("o"),
                ["FollowEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelCheer eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.CheererId ?? string.Empty,
                ["Name"] = eventArgs.CheererLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.CheererName ?? string.Empty,
                ["User"] = eventArgs.CheererName ?? string.Empty,
                ["Message"] = eventArgs.Message ?? string.Empty,
                ["Amount"] = eventArgs.Bits.ToString(),
                ["IsAnonymous"] = eventArgs.IsAnonymous.ToString(),
                ["CheerEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelSubscribe eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["Count"] = string.Empty,
                ["Streak"] = string.Empty,
                ["Tier"] = eventArgs.Tier ?? string.Empty,
                ["IsGift"] = "false",
                ["IsRenewal"] = "false",
                ["HadPreviousSub"] = "false",
                ["Message"] = string.Empty,
                ["SubscriptionEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelSubscriptionRenewal eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["Count"] = string.Empty,
                ["Streak"] = eventArgs.StreakMonths.ToString() ?? string.Empty,
                ["Tier"] = eventArgs.Tier ?? string.Empty,
                ["IsGift"] = "false",
                ["IsRenewal"] = "true",
                ["HadPreviousSub"] = "true",
                ["Message"] = eventArgs.Message?.Text ?? string.Empty,
                ["SubscriptionEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelSubscriptionGift eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["GiftAmount"] = eventArgs.Total.ToString(),
                ["TotalGifted"] = eventArgs.CumulativeTotal?.ToString() ?? string.Empty,
                ["SubscriptionGiftEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelSubscriptionEnd eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["User"] = eventArgs.UserLogin ?? string.Empty,
                ["SubscriptionEndEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelBan eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["User"] = eventArgs.UserLogin ?? string.Empty,
                ["IsUnBan"] = "false",
                ["BanEndsAt"] = eventArgs.EndsAt?.ToString("o") ?? string.Empty,
                ["BanEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelUnban eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["User"] = eventArgs.UserLogin ?? string.Empty,
                ["IsUnBan"] = "true",
                ["BanEndsAt"] = string.Empty,
                ["BanEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelBitsUse eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Name"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["Amount"] = eventArgs.Bits.ToString(),
                ["Bits"] = eventArgs.Bits.ToString(),
                ["Message"] = eventArgs.Message?.Text ?? string.Empty,
                ["Type"] = string.Empty,
                ["IsPowerUp"] = "false",
                ["PowerUpType"] = string.Empty,
                ["IsCustomPowerUp"] = "false",
                ["CustomPowerUpTitle"] = string.Empty,
                ["CustomPowerUpRewardId"] = string.Empty,
                ["HasBitsMessage"] = (eventArgs.Message != null).ToString(),
                ["BitsUseEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelAdBreakBegin eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Length"] = eventArgs.DurationSeconds.ToString(),
                ["Automatic"] = eventArgs.IsAutomatic.ToString(),
                ["StartedAt"] = eventArgs.StartedAt.ToString("o"),
                ["AdBreakStartEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }

        public static ConcurrentDictionary<string, string> ToDictionary(EventSubChannel.ChannelPointRedemption eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["Sender"] = eventArgs.UserName ?? string.Empty,
                ["Name"] = eventArgs.UserName ?? string.Empty,
                ["Username"] = eventArgs.UserLogin ?? string.Empty,
                ["DisplayName"] = eventArgs.UserName ?? string.Empty,
                ["User"] = eventArgs.UserName ?? string.Empty,
                ["Title"] = eventArgs.Reward?.Title ?? string.Empty,
                ["RewardName"] = eventArgs.Reward?.Title ?? string.Empty,
                ["UserInput"] = eventArgs.UserInput ?? string.Empty,
                ["Message"] = eventArgs.UserInput ?? string.Empty,
                ["ChannelPointRedeemEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            return dictionary;
        }
    }
}

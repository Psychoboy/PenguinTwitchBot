using DotNetTwitchBot.Bot.Events;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Actions.Utilities
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

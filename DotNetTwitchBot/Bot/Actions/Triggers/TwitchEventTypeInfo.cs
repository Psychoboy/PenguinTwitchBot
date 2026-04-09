using System.ComponentModel;
using System.Globalization;

namespace DotNetTwitchBot.Bot.Actions.Triggers
{
    /// <summary>
    /// Provides information about available Twitch events and their configurations
    /// </summary>
    public static class TwitchEventTypeInfo
    {
        public static List<TwitchEventInfo> GetAvailableEvents()
        {
            var events = new List<TwitchEventInfo>
            {
                new TwitchEventInfo
                {
                    Name = "ChannelFollow",
                    DisplayName = "Channel Follow",
                    Description = "Triggers when someone follows the channel",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Username", "DisplayName", "User", "FollowDate"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelCheer",
                    DisplayName = "Channel Cheer",
                    Description = "Triggers when someone cheers bits",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "MinAmount", "MaxAmount" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "DisplayName", "User", "Message", "Amount", "IsAnonymous"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelSubscribe",
                    DisplayName = "Channel Subscribe",
                    Description = "Triggers when someone subscribes (new subscription)",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "SubscriptionTiers" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "DisplayName", "User", "Tier", "IsGift", "HadPreviousSub"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelSubscriptionMessage",
                    DisplayName = "Channel Subscription Renewal",
                    Description = "Triggers when someone renews their subscription",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "SubscriptionTiers" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "DisplayName", "User", "Count", "Streak", "Tier", "Message", "HadPreviousSub"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelSubscriptionGift",
                    DisplayName = "Channel Subscription Gift",
                    Description = "Triggers when someone gifts subscriptions",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "MinGiftAmount", "MaxGiftAmount" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "DisplayName", "User", "GiftAmount", "TotalGifted"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelSubscriptionEnd",
                    DisplayName = "Channel Subscription End",
                    Description = "Triggers when a subscription ends",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "User"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelPointsCustomRewardRedemptionAdd",
                    DisplayName = "Channel Point Redemption",
                    Description = "Triggers when someone redeems a channel point reward",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "RewardTitles" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Sender", "Username", "DisplayName", "User", "Title", "RewardName", "UserInput", "Message"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelRaid",
                    DisplayName = "Channel Raid",
                    Description = "Triggers when the channel is raided",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "MinViewers", "MaxViewers" },
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "DisplayName", "User", "NumberOfViewers", "Viewers"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "StreamOnline",
                    DisplayName = "Stream Online",
                    Description = "Triggers when the stream goes online",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "EventType", "Timestamp"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "StreamOffline",
                    DisplayName = "Stream Offline",
                    Description = "Triggers when the stream goes offline",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "EventType", "Timestamp"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelBan",
                    DisplayName = "Channel Ban",
                    Description = "Triggers when someone is banned",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "User", "IsUnBan", "BanEndsAt"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelUnban",
                    DisplayName = "Channel Unban",
                    Description = "Triggers when someone is unbanned",
                    SupportsFiltering = false,
                    AvailableVariables = new List<string>
                    {
                        "UserId", "Name", "User", "IsUnBan"
                    }
                },
                new TwitchEventInfo
                {
                    Name = "ChannelAdBreakBegin",
                    DisplayName = "Ad Break Begin",
                    Description = "Triggers when an ad break begins",
                    SupportsFiltering = true,
                    FilterOptions = new List<string> { "IsAutomatic", "MinAdDuration", "MaxAdDuration" },
                    AvailableVariables = new List<string>
                    {
                        "Length", "Automatic", "StartedAt"
                    }
                }
            };

            // Sort by DisplayName alphabetically
            return events.OrderBy(e => e.DisplayName).ToList();
        }

        public static TwitchEventInfo? GetEventInfo(string eventName)
        {
            return GetAvailableEvents().FirstOrDefault(e => 
                e.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class TwitchEventInfo : IEquatable<TwitchEventInfo>
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool SupportsFiltering { get; set; }
        public List<string> FilterOptions { get; set; } = new();
        public List<string> AvailableVariables { get; set; } = new();

        public bool Equals(TwitchEventInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TwitchEventInfo);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}

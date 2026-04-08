using System.Collections.Concurrent;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Actions.Triggers
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

            return true;
        }
    }
}

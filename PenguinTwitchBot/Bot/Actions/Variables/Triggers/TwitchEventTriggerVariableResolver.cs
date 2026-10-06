using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for TwitchEvent triggers (Follow, Cheer, Sub, Channel Points, Ad Break, Ban, Raid, Stream Online/Offline, Bits Use, Chat Notification).
/// </summary>
public class TwitchEventTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType == TriggerTypes.TwitchEvent;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();

        if ((string.IsNullOrWhiteSpace(trigger.Name) || trigger.Name.Equals("TwitchEvent", StringComparison.OrdinalIgnoreCase)) &&
            TwitchEventTriggerConfig.TryFromJson(trigger.Configuration, out var config) &&
            !string.IsNullOrWhiteSpace(config?.EventName))
        {
            triggerName = config.EventName;
        }

        var source = sourcePrefix != null ? $"{sourcePrefix} (Event: {triggerName})" : $"Trigger: Twitch Event ({triggerName})";

        if (triggerName.Contains("AdBreak", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelAdBreakBegin
            variables.SetVariable("Length", "Length of the ad break in seconds", source, category, "90");
            variables.SetVariable("DurationSeconds", "Length of the ad break in seconds (alias for Length)", source, category, "90");
            variables.SetVariable("Automatic", "True if ad break was scheduled automatically, false if manual", source, category, "true");
            variables.SetVariable("IsAutomatic", "True if ad break was scheduled automatically (alias for Automatic)", source, category, "true");
            variables.SetVariable("StartedAt", "Timestamp when the ad break started (ISO 8601)", source, category, DateTime.UtcNow.ToString("o"));
        }
        else if (triggerName.Contains("Follow", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelFollow
            variables.SetVariable("UserId", "Twitch user ID of the follower", source, category, "12345678");
            variables.SetVariable("Username", "Twitch login username of the follower", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the follower", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The follower user (same as DisplayName)", source, category, "Penguin_Fan");
            variables.SetVariable("FollowDate", "Timestamp when the follow occurred (ISO 8601)", source, category, DateTime.UtcNow.ToString("o"));
        }
        else if (triggerName.Contains("BitsUse", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelBitsUse
            variables.SetVariable("UserId", "Twitch user ID of the user spending bits", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the user", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the user", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The user spending bits", source, category, "Penguin_Fan");
            variables.SetVariable("Amount", "Total bits spent", source, category, "500");
            variables.SetVariable("Bits", "Total bits spent (alias for Amount)", source, category, "500");
            variables.SetVariable("Message", "Chat message sent with the bits", source, category, "Cheer500 Great play!");
            variables.SetVariable("rawInput", "Sanitized chat message content", source, category, "Cheer500 Great play!");
            variables.SetVariable("Type", "Bits use type (cheer, power_up)", source, category, "cheer");
            variables.SetVariable("IsPowerUp", "True if bits were spent on a power-up", source, category, "false");
            variables.SetVariable("PowerUpType", "Type of power-up used", source, category, "message_effect");
            variables.SetVariable("IsCustomPowerUp", "True if bits were spent on a custom power-up reward", source, category, "false");
            variables.SetVariable("CustomPowerUpTitle", "Title of the custom power-up reward", source, category, "Confetti");
            variables.SetVariable("CustomPowerUpRewardId", "Reward ID of the custom power-up", source, category, "reward-123");
            variables.SetVariable("HasBitsMessage", "True if a message was included with the bits", source, category, "true");
        }
        else if (triggerName.Contains("Cheer", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("Bits", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelCheer
            variables.SetVariable("UserId", "Twitch user ID of the cheerer", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the cheerer", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the cheerer", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The user who cheered", source, category, "Penguin_Fan");
            variables.SetVariable("Amount", "Number of bits cheered", source, category, "100");
            variables.SetVariable("Bits", "Total bits cheered (alias for Amount)", source, category, "100");
            variables.SetVariable("Message", "Chat message sent with the cheer", source, category, "Cheer100 Great stream!");
            variables.SetVariable("rawInput", "Sanitized chat message content", source, category, "Cheer100 Great stream!");
            variables.SetVariable("IsAnonymous", "True if the cheer was anonymous", source, category, "false");
        }
        else if (triggerName.Contains("Raid", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelRaid
            variables.SetVariable("UserId", "Twitch user ID of the raiding streamer", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the raiding streamer", source, category, "friendly_streamer");
            variables.SetVariable("DisplayName", "Twitch display name of the raiding streamer", source, category, "Friendly_Streamer");
            variables.SetVariable("User", "The raiding streamer", source, category, "Friendly_Streamer");
            variables.SetVariable("NumberOfViewers", "Count of raiding viewers joining the stream", source, category, "42");
            variables.SetVariable("Viewers", "Count of raiding viewers joining the stream (alias for NumberOfViewers)", source, category, "42");
        }
        else if (triggerName.Contains("SubscriptionGift", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("SubGift", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelSubscriptionGift
            variables.SetVariable("UserId", "Twitch user ID of the gifter", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the gifter", source, category, "generous_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the gifter", source, category, "Generous_Fan");
            variables.SetVariable("User", "The user who gifted subscriptions", source, category, "Generous_Fan");
            variables.SetVariable("GiftAmount", "Number of subscriptions gifted in this purchase", source, category, "5");
            variables.SetVariable("TotalGifted", "Lifetime total subscriptions gifted by this user", source, category, "50");
        }
        else if (triggerName.Contains("SubscriptionEnd", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("SubEnd", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelSubscriptionEnd
            variables.SetVariable("UserId", "Twitch user ID of the user whose subscription ended", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the user whose subscription ended", source, category, "former_sub");
            variables.SetVariable("User", "The user whose subscription ended", source, category, "former_sub");
        }
        else if (triggerName.Contains("SubscriptionMessage", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("Resub", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelSubscriptionMessage (Renewal)
            variables.SetVariable("UserId", "Twitch user ID of the renewing subscriber", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the subscriber", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the subscriber", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The renewing subscriber", source, category, "Penguin_Fan");
            variables.SetVariable("Count", "Cumulative total months subscribed", source, category, "12");
            variables.SetVariable("Months", "Cumulative total months subscribed (alias for Count)", source, category, "12");
            variables.SetVariable("Streak", "Consecutive subscription streak in months", source, category, "6");
            variables.SetVariable("Tier", "Subscription tier (Tier 1, Tier 2, Tier 3, Prime)", source, category, "Tier 1");
            variables.SetVariable("IsGift", "True if this subscription was gifted", source, category, "false");
            variables.SetVariable("IsRenewal", "True if this is a renewal", source, category, "true");
            variables.SetVariable("HadPreviousSub", "True if user had previously subscribed", source, category, "true");
            variables.SetVariable("Message", "Resubscription message content", source, category, "1 year sub anniversary!");
            variables.SetVariable("rawInput", "Sanitized resubscription message content", source, category, "1 year sub anniversary!");
        }
        else if (triggerName.Contains("Subscribe", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("Subscription", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelSubscribe
            variables.SetVariable("UserId", "Twitch user ID of the subscriber", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the subscriber", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the subscriber", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The subscribing user", source, category, "Penguin_Fan");
            variables.SetVariable("Tier", "Subscription tier (Tier 1, Tier 2, Tier 3, Prime)", source, category, "Tier 1");
            variables.SetVariable("Count", "Cumulative months subscribed", source, category, "1");
            variables.SetVariable("Months", "Cumulative months subscribed (alias for Count)", source, category, "1");
            variables.SetVariable("Streak", "Consecutive subscription streak in months", source, category, "1");
            variables.SetVariable("IsGift", "True if this subscription was gifted", source, category, "false");
            variables.SetVariable("IsRenewal", "True if this is a renewal", source, category, "false");
            variables.SetVariable("HadPreviousSub", "True if user had previously subscribed", source, category, "false");
            variables.SetVariable("Message", "Subscription message content", source, category, "Happy to subscribe!");
            variables.SetVariable("rawInput", "Sanitized subscription message content", source, category, "Happy to subscribe!");
        }
        else if (triggerName.Contains("Point", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("Reward", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelPointsCustomRewardRedemptionAdd
            variables.SetVariable("UserId", "Twitch user ID of the redeeming user", source, category, "12345678");
            variables.SetVariable("Sender", "Username of the redeeming user", source, category, "hydrate_fan");
            variables.SetVariable("Name", "Login name of the redeeming user", source, category, "hydrate_fan");
            variables.SetVariable("Username", "Username of the redeeming user", source, category, "hydrate_fan");
            variables.SetVariable("DisplayName", "Display name of the redeeming user", source, category, "Hydrate_Fan");
            variables.SetVariable("User", "The redeeming user", source, category, "Hydrate_Fan");
            variables.SetVariable("Title", "Title of the redeemed Channel Points custom reward", source, category, "Hydrate");
            variables.SetVariable("RewardName", "Title of the redeemed custom reward (alias for Title)", source, category, "Hydrate");
            variables.SetVariable("RewardTitle", "Title of the redeemed custom reward (alias for Title)", source, category, "Hydrate");
            variables.SetVariable("UserInput", "Viewer text entered when redeeming the reward", source, category, "Take a sip!");
            variables.SetVariable("Message", "Viewer text entered when redeeming the reward", source, category, "Take a sip!");
            variables.SetVariable("rawInput", "Sanitized user input", source, category, "Take a sip!");
        }
        else if (triggerName.Contains("Unban", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelUnban
            variables.SetVariable("UserId", "Twitch user ID of the unbanned user", source, category, "12345678");
            variables.SetVariable("Name", "Login name of the unbanned user", source, category, "unbanned_user");
            variables.SetVariable("User", "The unbanned user", source, category, "unbanned_user");
            variables.SetVariable("IsUnBan", "True if this is an unban event", source, category, "true");
        }
        else if (triggerName.Contains("Ban", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelBan
            variables.SetVariable("UserId", "Twitch user ID of the banned user", source, category, "12345678");
            variables.SetVariable("Name", "Login name of the banned user", source, category, "banned_user");
            variables.SetVariable("User", "The banned user", source, category, "banned_user");
            variables.SetVariable("IsUnBan", "False if banned, true if unbanned", source, category, "false");
            variables.SetVariable("BanEndsAt", "Expiration timestamp for timeout, or blank for permanent ban", source, category, "2026-10-06T18:10:00.0000000Z");
        }
        else if (triggerName.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            // StreamOnline
            variables.SetVariable("EventType", "Twitch event name (StreamOnline)", source, category, "StreamOnline");
            variables.SetVariable("Timestamp", "Timestamp when broadcast went live (ISO 8601)", source, category, DateTime.UtcNow.ToString("o"));
            variables.SetVariable("StreamStartedAt", "Timestamp when broadcast went live (alias for Timestamp)", source, category, DateTime.UtcNow.ToString("o"));
        }
        else if (triggerName.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            // StreamOffline
            variables.SetVariable("EventType", "Twitch event name (StreamOffline)", source, category, "StreamOffline");
            variables.SetVariable("Timestamp", "Timestamp when broadcast ended (ISO 8601)", source, category, DateTime.UtcNow.ToString("o"));
        }
        else if (triggerName.Contains("ChatNotification", StringComparison.OrdinalIgnoreCase) || triggerName.Contains("Notification", StringComparison.OrdinalIgnoreCase))
        {
            // ChannelChatNotification
            variables.SetVariable("UserId", "Twitch user ID of the chatter", source, category, "12345678");
            variables.SetVariable("Name", "Login name of the chatter", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Display name of the chatter", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The chatter in the notification", source, category, "Penguin_Fan");
            variables.SetVariable("IsAnonymous", "True if the notification sender was anonymous", source, category, "false");
            variables.SetVariable("NoticeType", "Notice type (sub, resub, sub_gift, community_sub_gift, raid, announcement, etc.)", source, category, "sub");
            variables.SetVariable("SystemMessage", "Twitch system notification message", source, category, "User subscribed at Tier 1.");
            variables.SetVariable("Message", "Chat message text attached to the notification", source, category, "Hello world!");
            variables.SetVariable("rawInput", "Sanitized chat message text", source, category, "Hello world!");
        }
        else
        {
            // Generic fallback
            variables.SetVariable("User", "The user initiating or targeted in the event", source, category, "chatter_name");
            variables.SetVariable("UserName", "The login username of the chatter", source, category, "chatter_name");
            variables.SetVariable("DisplayName", "The display name of the chatter", source, category, "Chatter_Name");
        }
    }
}


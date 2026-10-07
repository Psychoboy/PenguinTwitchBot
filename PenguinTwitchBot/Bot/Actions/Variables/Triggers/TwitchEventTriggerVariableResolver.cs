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

        TwitchEventTriggerConfig? config = null;
        if (!string.IsNullOrWhiteSpace(trigger.Configuration))
        {
            TwitchEventTriggerConfig.TryFromJson(trigger.Configuration, out config);
        }

        if ((string.IsNullOrWhiteSpace(trigger.Name) || trigger.Name.Equals("TwitchEvent", StringComparison.OrdinalIgnoreCase)) &&
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
            // Common fields - populated for all chat notification events
            variables.SetVariable("UserId", "Twitch user ID of the chatter", source, category, "12345678");
            variables.SetVariable("Name", "Twitch login name of the chatter", source, category, "penguin_fan");
            variables.SetVariable("DisplayName", "Twitch display name of the chatter", source, category, "Penguin_Fan");
            variables.SetVariable("User", "The chatter in the notification (same as DisplayName)", source, category, "Penguin_Fan");
            variables.SetVariable("IsAnonymous", "True if the notification sender was anonymous", source, category, "false");
            variables.SetVariable("NoticeType", "Type of chat notification (sub, resub, sub_gift, community_sub_gift, gift_paid_upgrade, prime_paid_upgrade, raid, pay_it_forward, announcement, charity_donation, bits_badge_tier, watch_streak)", source, category, "sub");
            variables.SetVariable("SystemMessage", "Twitch system notification message displayed in chat", source, category, "Penguin_Fan subscribed at Tier 1.");
            variables.SetVariable("Message", "User chat message text attached to the notification", source, category, "Loving the stream!");
            variables.SetVariable("rawInput", "Sanitized chat message text", source, category, "Loving the stream!");

            bool shouldInclude(string noticeType) =>
                config?.NoticeTypes == null || config.NoticeTypes.Count == 0 ||
                config.NoticeTypes.Any(nt => nt.Equals(noticeType, StringComparison.OrdinalIgnoreCase));

            // Sub (populated when NoticeType is 'sub')
            if (shouldInclude("sub"))
            {
                variables.SetVariable("Sub.SubTier", "Subscription tier when NoticeType is 'sub' (1000 = Tier 1, 2000 = Tier 2, 3000 = Tier 3, Prime)", source, category, "1000");
                variables.SetVariable("Sub.DurationMonths", "Duration in months for the subscription (populated when NoticeType is 'sub')", source, category, "1");
                variables.SetVariable("Sub.IsPrime", "True if the subscription was made using Twitch Prime (populated when NoticeType is 'sub')", source, category, "false");
            }

            // Resub (populated when NoticeType is 'resub')
            if (shouldInclude("resub"))
            {
                variables.SetVariable("Resub.CumulativeMonths", "Cumulative total months the user has subscribed (populated when NoticeType is 'resub')", source, category, "12");
                variables.SetVariable("Resub.DurationMonths", "Duration in months for this renewal (populated when NoticeType is 'resub')", source, category, "1");
                variables.SetVariable("Resub.StreakMonths", "Consecutive subscription streak in months, if shared (populated when NoticeType is 'resub')", source, category, "6");
                variables.SetVariable("Resub.SubTier", "Subscription tier (1000, 2000, 3000, Prime) when NoticeType is 'resub'", source, category, "1000");
                variables.SetVariable("Resub.IsPrime", "True if renewed using Twitch Prime (populated when NoticeType is 'resub')", source, category, "false");
                variables.SetVariable("Resub.IsGift", "True if this subscription was originally a gift (populated when NoticeType is 'resub')", source, category, "false");
                variables.SetVariable("Resub.GifterIsAnonymous", "True if original gifter was anonymous (populated when NoticeType is 'resub')", source, category, "false");
                variables.SetVariable("Resub.GifterUserId", "Twitch user ID of the original gifter (populated when NoticeType is 'resub')", source, category, "87654321");
                variables.SetVariable("Resub.GifterUserName", "Display name of the original gifter (populated when NoticeType is 'resub')", source, category, "GenerousGifter");
                variables.SetVariable("Resub.GifterUserLogin", "Login name of the original gifter (populated when NoticeType is 'resub')", source, category, "generousgifter");
            }

            // SubGift (populated when NoticeType is 'sub_gift')
            if (shouldInclude("sub_gift"))
            {
                variables.SetVariable("SubGift.SubTier", "Subscription tier of the gift (1000, 2000, 3000) when NoticeType is 'sub_gift'", source, category, "1000");
                variables.SetVariable("SubGift.DurationMonths", "Number of months gifted (populated when NoticeType is 'sub_gift')", source, category, "1");
                variables.SetVariable("SubGift.CumulativeTotal", "Lifetime cumulative subscriptions gifted by this user (populated when NoticeType is 'sub_gift')", source, category, "25");
                variables.SetVariable("SubGift.RecipientUserId", "Twitch user ID of the recipient receiving the gift (populated when NoticeType is 'sub_gift')", source, category, "23456789");
                variables.SetVariable("SubGift.RecipientUserName", "Display name of the recipient receiving the gift (populated when NoticeType is 'sub_gift')", source, category, "LuckyViewer");
                variables.SetVariable("SubGift.RecipientUserLogin", "Login name of the recipient receiving the gift (populated when NoticeType is 'sub_gift')", source, category, "luckyviewer");
                variables.SetVariable("SubGift.CommunityGiftId", "ID linking this gift to a community gift bundle, if applicable (populated when NoticeType is 'sub_gift')", source, category, "batch-1234");
            }

            // CommunitySubGift (populated when NoticeType is 'community_sub_gift')
            if (shouldInclude("community_sub_gift"))
            {
                variables.SetVariable("CommunitySubGift.Id", "Unique ID for the community gift batch (populated when NoticeType is 'community_sub_gift')", source, category, "comm-batch-5678");
                variables.SetVariable("CommunitySubGift.Total", "Number of subscriptions gifted in this batch (populated when NoticeType is 'community_sub_gift')", source, category, "5");
                variables.SetVariable("CommunitySubGift.SubTier", "Subscription tier of the community gifts (1000, 2000, 3000) when NoticeType is 'community_sub_gift'", source, category, "1000");
                variables.SetVariable("CommunitySubGift.CumulativeTotal", "Lifetime cumulative subscriptions gifted by this user (populated when NoticeType is 'community_sub_gift')", source, category, "50");
            }

            // GiftPaidUpgrade (populated when NoticeType is 'gift_paid_upgrade')
            if (shouldInclude("gift_paid_upgrade"))
            {
                variables.SetVariable("GiftPaidUpgrade.GifterIsAnonymous", "True if the original gifter was anonymous (populated when NoticeType is 'gift_paid_upgrade')", source, category, "false");
                variables.SetVariable("GiftPaidUpgrade.GifterUserId", "Twitch user ID of the original gifter (populated when NoticeType is 'gift_paid_upgrade')", source, category, "87654321");
                variables.SetVariable("GiftPaidUpgrade.GifterUserName", "Display name of the original gifter (populated when NoticeType is 'gift_paid_upgrade')", source, category, "OriginalGifter");
                variables.SetVariable("GiftPaidUpgrade.GifterUserLogin", "Login name of the original gifter (populated when NoticeType is 'gift_paid_upgrade')", source, category, "originalgifter");
            }

            // PrimePaidUpgrade (populated when NoticeType is 'prime_paid_upgrade')
            if (shouldInclude("prime_paid_upgrade"))
            {
                variables.SetVariable("PrimePaidUpgrade.SubTier", "Subscription tier upgraded to from Prime (1000, 2000, 3000) when NoticeType is 'prime_paid_upgrade'", source, category, "1000");
            }

            // Raid (populated when NoticeType is 'raid')
            if (shouldInclude("raid"))
            {
                variables.SetVariable("Raid.UserId", "Twitch user ID of the raiding streamer (populated when NoticeType is 'raid')", source, category, "34567890");
                variables.SetVariable("Raid.UserName", "Display name of the raiding streamer (populated when NoticeType is 'raid')", source, category, "FriendlyStreamer");
                variables.SetVariable("Raid.UserLogin", "Login name of the raiding streamer (populated when NoticeType is 'raid')", source, category, "friendlystreamer");
                variables.SetVariable("Raid.ViewerCount", "Number of viewers in the raid (populated when NoticeType is 'raid')", source, category, "45");
                variables.SetVariable("Raid.ProfileImageUrl", "Profile image URL of the raiding streamer (populated when NoticeType is 'raid')", source, category, "https://static-cdn.jtvnw.net/...");
            }

            // PayItForward (populated when NoticeType is 'pay_it_forward')
            if (shouldInclude("pay_it_forward"))
            {
                variables.SetVariable("PayItForward.GifterIsAnonymous", "True if original gifter was anonymous (populated when NoticeType is 'pay_it_forward')", source, category, "false");
                variables.SetVariable("PayItForward.GifterUserId", "Twitch user ID of the original gifter (populated when NoticeType is 'pay_it_forward')", source, category, "87654321");
                variables.SetVariable("PayItForward.GifterUserName", "Display name of the original gifter (populated when NoticeType is 'pay_it_forward')", source, category, "OriginalGifter");
                variables.SetVariable("PayItForward.GifterUserLogin", "Login name of the original gifter (populated when NoticeType is 'pay_it_forward')", source, category, "originalgifter");
                variables.SetVariable("PayItForward.RecipientUserId", "Twitch user ID of the new recipient (populated when NoticeType is 'pay_it_forward')", source, category, "45678901");
                variables.SetVariable("PayItForward.RecipientUserName", "Display name of the new recipient (populated when NoticeType is 'pay_it_forward')", source, category, "ForwardRecipient");
                variables.SetVariable("PayItForward.RecipientUserLogin", "Login name of the new recipient (populated when NoticeType is 'pay_it_forward')", source, category, "forwardrecipient");
            }

            // Announcement (populated when NoticeType is 'announcement')
            if (shouldInclude("announcement"))
            {
                variables.SetVariable("Announcement.Color", "Highlight color of the announcement (blue, green, orange, purple, primary) when NoticeType is 'announcement'", source, category, "primary");
            }

            // CharityDonation (populated when NoticeType is 'charity_donation')
            if (shouldInclude("charity_donation"))
            {
                variables.SetVariable("CharityDonation.CharityName", "Name of the charity receiving the donation (populated when NoticeType is 'charity_donation')", source, category, "St. Jude Children's Research Hospital");
                variables.SetVariable("CharityDonation.AmountValue", "Donation amount in minor currency units (e.g. 500 = $5.00) when NoticeType is 'charity_donation'", source, category, "500");
                variables.SetVariable("CharityDonation.AmountDecimalPlaces", "Decimal places for the donation currency (populated when NoticeType is 'charity_donation')", source, category, "2");
                variables.SetVariable("CharityDonation.AmountCurrency", "Three-letter ISO currency code (e.g. USD) when NoticeType is 'charity_donation'", source, category, "USD");
            }

            // BitsBadgeTier (populated when NoticeType is 'bits_badge_tier')
            if (shouldInclude("bits_badge_tier"))
            {
                variables.SetVariable("BitsBadgeTier.Tier", "Threshold tier for the newly unlocked Bits badge (e.g. 100, 1000, 10000) when NoticeType is 'bits_badge_tier'", source, category, "1000");
            }

            // WatchStreak (populated when NoticeType is 'watch_streak')
            if (shouldInclude("watch_streak"))
            {
                variables.SetVariable("WatchStreak.StreakCount", "Number of consecutive streams the user has watched (populated when NoticeType is 'watch_streak')", source, category, "5");
                variables.SetVariable("WatchStreak.ChannelPointsAwarded", "Channel points awarded for the watch streak (populated when NoticeType is 'watch_streak')", source, category, "350");
            }
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


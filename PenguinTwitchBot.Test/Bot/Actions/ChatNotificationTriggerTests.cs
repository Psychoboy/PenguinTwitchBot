using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Events;

namespace PenguinTwitchBot.Test.Bot.Actions;

/// <summary>
/// Tests for TwitchEventTriggerConfig.Matches() covering all ChannelChatNotification filter logic.
/// </summary>
public class ChatNotificationTriggerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ConcurrentDictionary<string, string> Vars(params (string key, string value)[] pairs)
    {
        var dict = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return dict;
    }

    /// <summary>Creates a ChannelChatNotification config with the supplied notice types pre-selected.</summary>
    private static TwitchEventTriggerConfig Config(params string[] noticeTypes)
    {
        var cfg = new TwitchEventTriggerConfig { EventName = "ChannelChatNotification" };
        cfg.NoticeTypes.AddRange(noticeTypes);
        return cfg;
    }

    // -----------------------------------------------------------------------
    // NoticeType requirement
    // -----------------------------------------------------------------------

    [Fact]
    public void NoNoticeTypes_ChannelChatNotification_AlwaysFails()
    {
        var cfg = new TwitchEventTriggerConfig { EventName = "ChannelChatNotification" };
        Assert.False(cfg.Matches(Vars(("NoticeType", "sub"))));
    }

    [Fact]
    public void NoNoticeTypes_OtherEvent_DoesNotBlock()
    {
        // Other event types are not gated by the ChannelChatNotification NoticeType requirement
        var cfg = new TwitchEventTriggerConfig { EventName = "ChannelSubscribed" };
        Assert.True(cfg.Matches(Vars(("Tier", "1000"))));
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("resub")]
    [InlineData("sub_gift")]
    [InlineData("community_sub_gift")]
    [InlineData("gift_paid_upgrade")]
    [InlineData("prime_paid_upgrade")]
    [InlineData("raid")]
    [InlineData("unraid")]
    [InlineData("pay_it_forward")]
    [InlineData("announcement")]
    [InlineData("bits_badge_tier")]
    [InlineData("charity_donation")]
    [InlineData("watch_streak")]
    public void ExactNoticeType_Matches(string noticeType)
    {
        var cfg = Config(noticeType);
        Assert.True(cfg.Matches(Vars(("NoticeType", noticeType))));
    }

    [Fact]
    public void WrongNoticeType_DoesNotMatch()
    {
        var cfg = Config("sub");
        Assert.False(cfg.Matches(Vars(("NoticeType", "resub"))));
    }

    [Fact]
    public void MultipleNoticeTypes_AnyMatch_Passes()
    {
        var cfg = Config("sub", "resub");
        Assert.True(cfg.Matches(Vars(("NoticeType", "sub"))));
        Assert.True(cfg.Matches(Vars(("NoticeType", "resub"))));
        Assert.False(cfg.Matches(Vars(("NoticeType", "raid"))));
    }

    [Fact]
    public void NoticeType_ValueCaseInsensitive()
    {
        var cfg = Config("sub");
        Assert.True(cfg.Matches(Vars(("NoticeType", "SUB"))));
    }

    // -----------------------------------------------------------------------
    // WatchStreak range
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(5, null, 4, false)]  // below min
    [InlineData(5, null, 5, true)]   // at min
    [InlineData(5, null, 99, true)]  // above min
    [InlineData(null, 10, 11, false)] // above max
    [InlineData(null, 10, 10, true)]  // at max
    [InlineData(null, 10, 1, true)]   // below max
    [InlineData(5, 10, 7, true)]      // in range
    [InlineData(5, 10, 4, false)]     // below range
    [InlineData(5, 10, 11, false)]    // above range
    public void WatchStreak_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("watch_streak");
        cfg.MinWatchStreak = min;
        cfg.MaxWatchStreak = max;
        var result = cfg.Matches(Vars(("NoticeType", "watch_streak"), ("WatchStreak.StreakCount", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WatchStreak_MissingVariable_Fails()
    {
        var cfg = Config("watch_streak");
        cfg.MinWatchStreak = 5;
        Assert.False(cfg.Matches(Vars(("NoticeType", "watch_streak"))));
    }

    [Fact]
    public void WatchStreak_NoRange_NoFilter()
    {
        var cfg = Config("watch_streak");
        // No min/max set — any streak value passes
        Assert.True(cfg.Matches(Vars(("NoticeType", "watch_streak"), ("WatchStreak.StreakCount", "1"))));
    }

    // -----------------------------------------------------------------------
    // Raid viewer count range
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(100, null, 50, false)]   // below min
    [InlineData(100, null, 100, true)]   // at min
    [InlineData(100, null, 500, true)]   // above min
    [InlineData(null, 500, 501, false)]  // above max
    [InlineData(null, 500, 500, true)]   // at max
    [InlineData(100, 500, 250, true)]    // in range
    [InlineData(100, 500, 99, false)]    // below range
    [InlineData(100, 500, 501, false)]   // above range
    public void RaidViewers_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("raid");
        cfg.MinRaidViewers = min;
        cfg.MaxRaidViewers = max;
        var result = cfg.Matches(Vars(("NoticeType", "raid"), ("Raid.ViewerCount", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RaidViewers_MissingVariable_Fails()
    {
        var cfg = Config("raid");
        cfg.MinRaidViewers = 100;
        Assert.False(cfg.Matches(Vars(("NoticeType", "raid"))));
    }

    // -----------------------------------------------------------------------
    // SubTiers filter
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("sub", "Sub.SubTier")]
    [InlineData("resub", "Resub.SubTier")]
    [InlineData("sub_gift", "SubGift.SubTier")]
    [InlineData("community_sub_gift", "CommunitySubGift.SubTier")]
    [InlineData("prime_paid_upgrade", "PrimePaidUpgrade.SubTier")]
    public void SubTiers_MatchingTier_Passes(string noticeType, string varKey)
    {
        var cfg = Config(noticeType);
        cfg.SubTiers.Add("1000");
        Assert.True(cfg.Matches(Vars(("NoticeType", noticeType), (varKey, "1000"))));
    }

    [Theory]
    [InlineData("sub", "Sub.SubTier")]
    [InlineData("resub", "Resub.SubTier")]
    [InlineData("sub_gift", "SubGift.SubTier")]
    [InlineData("community_sub_gift", "CommunitySubGift.SubTier")]
    [InlineData("prime_paid_upgrade", "PrimePaidUpgrade.SubTier")]
    public void SubTiers_NonMatchingTier_Fails(string noticeType, string varKey)
    {
        var cfg = Config(noticeType);
        cfg.SubTiers.Add("1000");
        Assert.False(cfg.Matches(Vars(("NoticeType", noticeType), (varKey, "2000"))));
    }

    [Fact]
    public void SubTiers_MultipleTiersAllowed()
    {
        var cfg = Config("sub");
        cfg.SubTiers.AddRange(["1000", "2000"]);
        Assert.True(cfg.Matches(Vars(("NoticeType", "sub"), ("Sub.SubTier", "1000"))));
        Assert.True(cfg.Matches(Vars(("NoticeType", "sub"), ("Sub.SubTier", "2000"))));
        Assert.False(cfg.Matches(Vars(("NoticeType", "sub"), ("Sub.SubTier", "3000"))));
    }

    [Fact]
    public void SubTiers_NoticeTypeWithNoSubTierKey_Fails()
    {
        // raid events don't have a sub tier, so SubTiers filter must reject them
        var cfg = Config("raid");
        cfg.SubTiers.Add("1000");
        Assert.False(cfg.Matches(Vars(("NoticeType", "raid"), ("Raid.ViewerCount", "100"))));
    }

    [Fact]
    public void SubTiers_MissingTierVariable_Fails()
    {
        var cfg = Config("sub");
        cfg.SubTiers.Add("1000");
        Assert.False(cfg.Matches(Vars(("NoticeType", "sub")))); // Sub.SubTier absent
    }

    [Fact]
    public void SubTiers_Empty_NoFilter()
    {
        var cfg = Config("sub");
        // No tiers configured — any tier passes
        Assert.True(cfg.Matches(Vars(("NoticeType", "sub"), ("Sub.SubTier", "3000"))));
    }

    // -----------------------------------------------------------------------
    // CumulativeMonths range (resub)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(12, null, 6, false)]    // below min
    [InlineData(12, null, 12, true)]    // at min
    [InlineData(12, null, 100, true)]   // above min
    [InlineData(null, 24, 25, false)]   // above max
    [InlineData(null, 24, 24, true)]    // at max
    [InlineData(12, 24, 18, true)]      // in range
    [InlineData(12, 24, 11, false)]     // below range
    [InlineData(12, 24, 25, false)]     // above range
    public void CumulativeMonths_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("resub");
        cfg.MinCumulativeMonths = min;
        cfg.MaxCumulativeMonths = max;
        var result = cfg.Matches(Vars(("NoticeType", "resub"), ("Resub.CumulativeMonths", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CumulativeMonths_MissingVariable_Fails()
    {
        var cfg = Config("resub");
        cfg.MinCumulativeMonths = 12;
        Assert.False(cfg.Matches(Vars(("NoticeType", "resub"))));
    }

    // -----------------------------------------------------------------------
    // CommunityGiftCount range (community_sub_gift)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(10, null, 5, false)]    // below min
    [InlineData(10, null, 10, true)]    // at min
    [InlineData(null, 50, 51, false)]   // above max
    [InlineData(null, 50, 50, true)]    // at max
    [InlineData(10, 50, 25, true)]      // in range
    [InlineData(10, 50, 9, false)]      // below range
    [InlineData(10, 50, 51, false)]     // above range
    public void CommunityGiftCount_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("community_sub_gift");
        cfg.MinCommunityGiftCount = min;
        cfg.MaxCommunityGiftCount = max;
        var result = cfg.Matches(Vars(("NoticeType", "community_sub_gift"), ("CommunitySubGift.Total", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CommunityGiftCount_MissingVariable_Fails()
    {
        var cfg = Config("community_sub_gift");
        cfg.MinCommunityGiftCount = 10;
        Assert.False(cfg.Matches(Vars(("NoticeType", "community_sub_gift"))));
    }

    // -----------------------------------------------------------------------
    // CharityAmount range (charity_donation, minor units)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(500, null, 499, false)]     // below min ($4.99)
    [InlineData(500, null, 500, true)]      // at min ($5.00)
    [InlineData(null, 10000, 10001, false)] // above max ($100.01)
    [InlineData(null, 10000, 10000, true)]  // at max ($100.00)
    [InlineData(500, 10000, 5000, true)]    // in range
    [InlineData(500, 10000, 499, false)]    // below range
    [InlineData(500, 10000, 10001, false)]  // above range
    public void CharityAmount_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("charity_donation");
        cfg.MinCharityAmount = min;
        cfg.MaxCharityAmount = max;
        var result = cfg.Matches(Vars(("NoticeType", "charity_donation"), ("CharityDonation.AmountValue", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CharityAmount_MissingVariable_Fails()
    {
        var cfg = Config("charity_donation");
        cfg.MinCharityAmount = 500;
        Assert.False(cfg.Matches(Vars(("NoticeType", "charity_donation"))));
    }

    // -----------------------------------------------------------------------
    // BitsBadgeTier range
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1000, null, 100, false)]    // below min
    [InlineData(1000, null, 1000, true)]    // at min
    [InlineData(null, 5000, 10000, false)]  // above max
    [InlineData(null, 5000, 5000, true)]    // at max
    [InlineData(1000, 5000, 1000, true)]    // at lower bound
    [InlineData(1000, 5000, 5000, true)]    // at upper bound
    [InlineData(1000, 5000, 999, false)]    // below range
    [InlineData(1000, 5000, 5001, false)]   // above range
    public void BitsBadgeTier_Range(int? min, int? max, int value, bool expected)
    {
        var cfg = Config("bits_badge_tier");
        cfg.MinBitsBadgeTier = min;
        cfg.MaxBitsBadgeTier = max;
        var result = cfg.Matches(Vars(("NoticeType", "bits_badge_tier"), ("BitsBadgeTier.Tier", value.ToString())));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BitsBadgeTier_MissingVariable_Fails()
    {
        var cfg = Config("bits_badge_tier");
        cfg.MinBitsBadgeTier = 1000;
        Assert.False(cfg.Matches(Vars(("NoticeType", "bits_badge_tier"))));
    }

    // -----------------------------------------------------------------------
    // AnnouncementColors filter
    // -----------------------------------------------------------------------

    [Fact]
    public void AnnouncementColor_MatchingColor_Passes()
    {
        var cfg = Config("announcement");
        cfg.AnnouncementColors.Add("blue");
        Assert.True(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "blue"))));
    }

    [Fact]
    public void AnnouncementColor_NonMatchingColor_Fails()
    {
        var cfg = Config("announcement");
        cfg.AnnouncementColors.Add("blue");
        Assert.False(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "green"))));
    }

    [Fact]
    public void AnnouncementColor_CaseInsensitive_Passes()
    {
        var cfg = Config("announcement");
        cfg.AnnouncementColors.Add("blue");
        Assert.True(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "BLUE"))));
    }

    [Fact]
    public void AnnouncementColor_MultipleColors_EitherMatches()
    {
        var cfg = Config("announcement");
        cfg.AnnouncementColors.AddRange(["blue", "green"]);
        Assert.True(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "blue"))));
        Assert.True(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "green"))));
        Assert.False(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "orange"))));
    }

    [Fact]
    public void AnnouncementColor_Empty_NoFilter()
    {
        var cfg = Config("announcement");
        // No colors configured — any color passes
        Assert.True(cfg.Matches(Vars(("NoticeType", "announcement"), ("Announcement.Color", "purple"))));
    }

    [Fact]
    public void AnnouncementColor_MissingVariable_Fails()
    {
        var cfg = Config("announcement");
        cfg.AnnouncementColors.Add("blue");
        Assert.False(cfg.Matches(Vars(("NoticeType", "announcement"))));
    }

    // -----------------------------------------------------------------------
    // Combined filters
    // -----------------------------------------------------------------------

    [Fact]
    public void Combined_ResubWithTierAndMonths_AllPass()
    {
        var cfg = Config("resub");
        cfg.SubTiers.Add("1000");
        cfg.MinCumulativeMonths = 6;
        cfg.MaxCumulativeMonths = 24;

        var vars = Vars(
            ("NoticeType", "resub"),
            ("Resub.SubTier", "1000"),
            ("Resub.CumulativeMonths", "12"));

        Assert.True(cfg.Matches(vars));
    }

    [Fact]
    public void Combined_ResubWithTierAndMonths_WrongTier_Fails()
    {
        var cfg = Config("resub");
        cfg.SubTiers.Add("1000");
        cfg.MinCumulativeMonths = 6;

        var vars = Vars(
            ("NoticeType", "resub"),
            ("Resub.SubTier", "3000"),
            ("Resub.CumulativeMonths", "12"));

        Assert.False(cfg.Matches(vars));
    }

    [Fact]
    public void Combined_ResubWithTierAndMonths_MonthsBelowMin_Fails()
    {
        var cfg = Config("resub");
        cfg.SubTiers.Add("1000");
        cfg.MinCumulativeMonths = 6;

        var vars = Vars(
            ("NoticeType", "resub"),
            ("Resub.SubTier", "1000"),
            ("Resub.CumulativeMonths", "3"));

        Assert.False(cfg.Matches(vars));
    }

    [Fact]
    public void Combined_WatchStreakAndNoticeType_WrongType_Fails()
    {
        // Config for watch_streak with a streak range — a "sub" event should not match
        var cfg = Config("watch_streak");
        cfg.MinWatchStreak = 5;

        Assert.False(cfg.Matches(Vars(("NoticeType", "sub"), ("WatchStreak.StreakCount", "10"))));
    }

    [Fact]
    public void SubTiers_SwitchCaseInsensitive_Passes()
    {
        // Verify the switch is normalized to lowercase so "SUB" doesn't produce a null key
        var cfg = Config("sub");
        cfg.SubTiers.Add("1000");
        Assert.True(cfg.Matches(Vars(("NoticeType", "SUB"), ("Sub.SubTier", "1000"))));
    }
}

/// <summary>
/// Tests for TwitchEventTriggerConfig.Matches() covering ChannelBitsUse filter logic:
/// BitsTypes, PowerUpTypes, CustomPowerUpTitles, CustomPowerUpRewardIds.
/// </summary>
public class ChannelBitsUseTriggerTests
{
    private static ConcurrentDictionary<string, string> Vars(params (string key, string value)[] pairs)
    {
        var dict = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return dict;
    }

    private static TwitchEventTriggerConfig BitsConfig() =>
        new TwitchEventTriggerConfig { EventName = "ChannelBitsUse" };

    // -----------------------------------------------------------------------
    // BitsTypes filter
    // -----------------------------------------------------------------------

    [Fact]
    public void BitsTypes_MatchingType_Passes()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("cheer");
        Assert.True(cfg.Matches(Vars(("Type", "cheer"))));
    }

    [Fact]
    public void BitsTypes_NonMatchingType_Fails()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("cheer");
        Assert.False(cfg.Matches(Vars(("Type", "power_up"))));
    }

    [Fact]
    public void BitsTypes_CaseInsensitive_Passes()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("cheer");
        Assert.True(cfg.Matches(Vars(("Type", "CHEER"))));
    }

    [Fact]
    public void BitsTypes_Multiple_AnyMatches()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.AddRange(["cheer", "power_up"]);
        Assert.True(cfg.Matches(Vars(("Type", "cheer"))));
        Assert.True(cfg.Matches(Vars(("Type", "power_up"))));
        Assert.False(cfg.Matches(Vars(("Type", "unknown"))));
    }

    [Fact]
    public void BitsTypes_MissingVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("cheer");
        Assert.False(cfg.Matches(Vars())); // No "Type" key
    }

    [Fact]
    public void BitsTypes_Empty_NoFilter()
    {
        var cfg = BitsConfig();
        // No types configured — any type passes
        Assert.True(cfg.Matches(Vars(("Type", "power_up"))));
    }

    // -----------------------------------------------------------------------
    // PowerUpTypes filter
    // -----------------------------------------------------------------------

    [Fact]
    public void PowerUpTypes_MatchingType_Passes()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.Add("message_effect");
        Assert.True(cfg.Matches(Vars(("PowerUpType", "message_effect"))));
    }

    [Fact]
    public void PowerUpTypes_NonMatchingType_Fails()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.Add("message_effect");
        Assert.False(cfg.Matches(Vars(("PowerUpType", "celebration"))));
    }

    [Fact]
    public void PowerUpTypes_CaseInsensitive_Passes()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.Add("message_effect");
        Assert.True(cfg.Matches(Vars(("PowerUpType", "MESSAGE_EFFECT"))));
    }

    [Fact]
    public void PowerUpTypes_Multiple_AnyMatches()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.AddRange(["message_effect", "celebration", "gigantify_an_emote"]);
        Assert.True(cfg.Matches(Vars(("PowerUpType", "celebration"))));
        Assert.False(cfg.Matches(Vars(("PowerUpType", "unknown_type"))));
    }

    [Fact]
    public void PowerUpTypes_MissingVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.Add("message_effect");
        Assert.False(cfg.Matches(Vars())); // No "PowerUpType" key
    }

    [Fact]
    public void PowerUpTypes_EmptyStringVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.PowerUpTypes.Add("message_effect");
        Assert.False(cfg.Matches(Vars(("PowerUpType", "")))); // empty string treated as missing
    }

    [Fact]
    public void PowerUpTypes_Empty_NoFilter()
    {
        var cfg = BitsConfig();
        Assert.True(cfg.Matches(Vars(("PowerUpType", "celebration"))));
    }

    // -----------------------------------------------------------------------
    // CustomPowerUpTitles filter
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomPowerUpTitles_MatchingTitle_Passes()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpTitles.Add("My Custom Power");
        Assert.True(cfg.Matches(Vars(("CustomPowerUpTitle", "My Custom Power"))));
    }

    [Fact]
    public void CustomPowerUpTitles_NonMatchingTitle_Fails()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpTitles.Add("My Custom Power");
        Assert.False(cfg.Matches(Vars(("CustomPowerUpTitle", "Other Power"))));
    }

    [Fact]
    public void CustomPowerUpTitles_CaseInsensitive_Passes()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpTitles.Add("My Custom Power");
        Assert.True(cfg.Matches(Vars(("CustomPowerUpTitle", "MY CUSTOM POWER"))));
    }

    [Fact]
    public void CustomPowerUpTitles_MissingVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpTitles.Add("My Custom Power");
        Assert.False(cfg.Matches(Vars())); // No "CustomPowerUpTitle" key
    }

    [Fact]
    public void CustomPowerUpTitles_EmptyStringVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpTitles.Add("My Custom Power");
        Assert.False(cfg.Matches(Vars(("CustomPowerUpTitle", "")))); // empty string treated as missing
    }

    [Fact]
    public void CustomPowerUpTitles_Empty_NoFilter()
    {
        var cfg = BitsConfig();
        Assert.True(cfg.Matches(Vars(("CustomPowerUpTitle", "Anything"))));
    }

    // -----------------------------------------------------------------------
    // CustomPowerUpRewardIds filter
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomPowerUpRewardIds_MatchingId_Passes()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpRewardIds.Add("abc-123");
        Assert.True(cfg.Matches(Vars(("CustomPowerUpRewardId", "abc-123"))));
    }

    [Fact]
    public void CustomPowerUpRewardIds_NonMatchingId_Fails()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpRewardIds.Add("abc-123");
        Assert.False(cfg.Matches(Vars(("CustomPowerUpRewardId", "xyz-999"))));
    }

    [Fact]
    public void CustomPowerUpRewardIds_CaseInsensitive_Passes()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpRewardIds.Add("abc-123");
        Assert.True(cfg.Matches(Vars(("CustomPowerUpRewardId", "ABC-123"))));
    }

    [Fact]
    public void CustomPowerUpRewardIds_Multiple_AnyMatches()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpRewardIds.AddRange(["abc-123", "def-456"]);
        Assert.True(cfg.Matches(Vars(("CustomPowerUpRewardId", "abc-123"))));
        Assert.True(cfg.Matches(Vars(("CustomPowerUpRewardId", "def-456"))));
        Assert.False(cfg.Matches(Vars(("CustomPowerUpRewardId", "ghi-789"))));
    }

    [Fact]
    public void CustomPowerUpRewardIds_MissingVariable_Fails()
    {
        var cfg = BitsConfig();
        cfg.CustomPowerUpRewardIds.Add("abc-123");
        Assert.False(cfg.Matches(Vars())); // No "CustomPowerUpRewardId" key
    }

    [Fact]
    public void CustomPowerUpRewardIds_Empty_NoFilter()
    {
        var cfg = BitsConfig();
        Assert.True(cfg.Matches(Vars(("CustomPowerUpRewardId", "any-id"))));
    }

    // -----------------------------------------------------------------------
    // Combined BitsUse filters
    // -----------------------------------------------------------------------

    [Fact]
    public void Combined_TypeAndPowerUp_BothMatch_Passes()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("power_up");
        cfg.PowerUpTypes.Add("message_effect");

        Assert.True(cfg.Matches(Vars(("Type", "power_up"), ("PowerUpType", "message_effect"))));
    }

    [Fact]
    public void Combined_TypeAndPowerUp_TypeFails_Fails()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("power_up");
        cfg.PowerUpTypes.Add("message_effect");

        Assert.False(cfg.Matches(Vars(("Type", "cheer"), ("PowerUpType", "message_effect"))));
    }

    [Fact]
    public void Combined_TypeAndCustomTitle_BothMatch_Passes()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("power_up");
        cfg.CustomPowerUpTitles.Add("My Power");

        Assert.True(cfg.Matches(Vars(("Type", "power_up"), ("CustomPowerUpTitle", "My Power"))));
    }

    [Fact]
    public void Combined_AllFourFilters_AllMatch_Passes()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("power_up");
        cfg.PowerUpTypes.Add("message_effect");
        cfg.CustomPowerUpTitles.Add("My Power");
        cfg.CustomPowerUpRewardIds.Add("reward-1");

        var vars = Vars(
            ("Type", "power_up"),
            ("PowerUpType", "message_effect"),
            ("CustomPowerUpTitle", "My Power"),
            ("CustomPowerUpRewardId", "reward-1"));

        Assert.True(cfg.Matches(vars));
    }

    [Fact]
    public void Combined_AllFourFilters_OneMissing_Fails()
    {
        var cfg = BitsConfig();
        cfg.BitsTypes.Add("power_up");
        cfg.PowerUpTypes.Add("message_effect");
        cfg.CustomPowerUpTitles.Add("My Power");
        cfg.CustomPowerUpRewardIds.Add("reward-1");

        // Missing CustomPowerUpRewardId
        var vars = Vars(
            ("Type", "power_up"),
            ("PowerUpType", "message_effect"),
            ("CustomPowerUpTitle", "My Power"));

        Assert.False(cfg.Matches(vars));
    }
}

/// <summary>
/// Tests for TwitchEventArgsConverter.ToDictionary(ChannelChatNotification) — verifies that all
/// expected dot-notation keys are produced and null sub-objects emit empty strings (not exceptions).
/// </summary>
public class ChatNotificationConverterTests
{
    [Fact]
    public void NullArgs_ReturnsEmptyDictionary()
    {
        var dict = TwitchEventArgsConverter.ToDictionary((PenguinTwitchBot.Bot.Events.ChatNotificationEventArgs)null!);
        Assert.NotNull(dict);
        Assert.Empty(dict);
    }

    [Fact]
    public void CommonFields_ArePopulated()
    {
        var args = new PenguinTwitchBot.Bot.Events.ChatNotificationEventArgs
        {
            UserId = "123",
            Name = "testlogin",
            DisplayName = "TestLogin",
            IsAnonymous = false,
            NoticeType = "sub",
            SystemMessage = "TestLogin subscribed",
            Message = "Hello!"
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("123", dict["UserId"]);
        Assert.Equal("testlogin", dict["Name"]);
        Assert.Equal("TestLogin", dict["DisplayName"]);
        Assert.Equal("TestLogin", dict["User"]);
        Assert.Equal("False", dict["IsAnonymous"]);
        Assert.Equal("sub", dict["NoticeType"]);
        Assert.Equal("TestLogin subscribed", dict["SystemMessage"]);
        Assert.Equal("Hello!", dict["Message"]);
    }

    [Fact]
    public void SubObject_PopulatesDotNotationKeys()
    {
         var args = new ChatNotificationEventArgs
        {
            NoticeType = "sub",
            Sub = new ChatNotificationSubInfo { SubTier = "1000", DurationMonths = 1, IsPrime = false }
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("1000", dict["Sub.SubTier"]);
        Assert.Equal("1", dict["Sub.DurationMonths"]);
        Assert.Equal("False", dict["Sub.IsPrime"]);
    }

    [Fact]
    public void ResubObject_PopulatesDotNotationKeys()
    {
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "resub",
            Resub = new ChatNotificationResubInfo
            {
                CumulativeMonths = 18,
                DurationMonths = 1,
                StreakMonths = 3,
                SubTier = "2000",
                IsPrime = false,
                IsGift = false
            }
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("18", dict["Resub.CumulativeMonths"]);
        Assert.Equal("1", dict["Resub.DurationMonths"]);
        Assert.Equal("3", dict["Resub.StreakMonths"]);
        Assert.Equal("2000", dict["Resub.SubTier"]);
    }

    [Fact]
    public void RaidObject_PopulatesDotNotationKeys()
    {
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "raid",
            Raid = new ChatNotificationRaidInfo
            {
                UserId = "456",
                UserName = "RaiderName",
                UserLogin = "raidername",
                ViewerCount = 250,
                ProfileImageUrl = "https://example.com/img.png"
            }
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("456", dict["Raid.UserId"]);
        Assert.Equal("RaiderName", dict["Raid.UserName"]);
        Assert.Equal("250", dict["Raid.ViewerCount"]);
        Assert.Equal("https://example.com/img.png", dict["Raid.ProfileImageUrl"]);
    }

    [Fact]
    public void WatchStreakObject_PopulatesDotNotationKeys()
    {
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "watch_streak",
            WatchStreak = new ChatNotificationWatchStreakInfo { StreakCount = 7, ChannelPointsAwarded = 350 }
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("7", dict["WatchStreak.StreakCount"]);
        Assert.Equal("350", dict["WatchStreak.ChannelPointsAwarded"]);
    }

    [Fact]
    public void NullSubObject_EmitsEmptyStringsForItsKeys()
    {
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "raid",
            Sub = null
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        // Sub keys should be present but empty when Sub is null
        Assert.Equal(string.Empty, dict["Sub.SubTier"]);
        Assert.Equal(string.Empty, dict["Sub.DurationMonths"]);
        Assert.Equal(string.Empty, dict["Sub.IsPrime"]);
    }

    [Fact]
    public void CharityDonationObject_PopulatesDotNotationKeys()
    {
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "charity_donation",
            CharityDonation = new ChatNotificationCharityDonationInfo
            {
                CharityName = "Some Charity",
                AmountValue = 5000,
                AmountDecimalPlaces = 2,
                AmountCurrency = "USD"
            }
        };

        var dict = TwitchEventArgsConverter.ToDictionary(args);

        Assert.Equal("Some Charity", dict["CharityDonation.CharityName"]);
        Assert.Equal("5000", dict["CharityDonation.AmountValue"]);
        Assert.Equal("2", dict["CharityDonation.AmountDecimalPlaces"]);
        Assert.Equal("USD", dict["CharityDonation.AmountCurrency"]);
    }

    [Fact]
    public void AllExpectedKeysArePresent()
    {
        // Verify that every documented key is emitted even with a default (all-null) args object
        var args = new ChatNotificationEventArgs
        {
            NoticeType = "sub"
        };
        var dict = TwitchEventArgsConverter.ToDictionary(args);

        var expectedKeys = new[]
        {
            "UserId", "Name", "DisplayName", "User", "IsAnonymous", "NoticeType", "SystemMessage", "Message",
            "Sub.SubTier", "Sub.DurationMonths", "Sub.IsPrime",
            "Resub.CumulativeMonths", "Resub.DurationMonths", "Resub.StreakMonths", "Resub.SubTier",
            "Resub.IsPrime", "Resub.IsGift", "Resub.GifterIsAnonymous", "Resub.GifterUserId",
            "Resub.GifterUserName", "Resub.GifterUserLogin",
            "SubGift.DurationMonths", "SubGift.CumulativeTotal", "SubGift.RecipientUserId",
            "SubGift.RecipientUserName", "SubGift.RecipientUserLogin", "SubGift.SubTier", "SubGift.CommunityGiftId",
            "CommunitySubGift.Id", "CommunitySubGift.Total", "CommunitySubGift.SubTier", "CommunitySubGift.CumulativeTotal",
            "GiftPaidUpgrade.GifterIsAnonymous", "GiftPaidUpgrade.GifterUserId",
            "GiftPaidUpgrade.GifterUserName", "GiftPaidUpgrade.GifterUserLogin",
            "PrimePaidUpgrade.SubTier",
            "Raid.UserId", "Raid.UserName", "Raid.UserLogin", "Raid.ViewerCount", "Raid.ProfileImageUrl",
            "PayItForward.GifterIsAnonymous", "PayItForward.GifterUserId",
            "PayItForward.GifterUserName", "PayItForward.GifterUserLogin",
            "PayItForward.RecipientUserId", "PayItForward.RecipientUserName", "PayItForward.RecipientUserLogin",
            "Announcement.Color",
            "CharityDonation.CharityName", "CharityDonation.AmountValue",
            "CharityDonation.AmountDecimalPlaces", "CharityDonation.AmountCurrency",
            "BitsBadgeTier.Tier",
            "WatchStreak.StreakCount", "WatchStreak.ChannelPointsAwarded",
            "ChatNotificationEventArgs"
        };

        foreach (var key in expectedKeys)
            Assert.True(dict.ContainsKey(key), $"Missing key: {key}");
    }
}

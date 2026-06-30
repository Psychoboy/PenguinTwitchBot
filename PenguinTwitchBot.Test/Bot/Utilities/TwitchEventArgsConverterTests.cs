using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Events;
using System;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Utilities
{
    public class TwitchEventArgsConverterTests
    {
        [Fact]
        public void ToDictionary_FollowEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((FollowEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_FollowEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new FollowEventArgs
            {
                UserId = "12345",
                Username = "testuser",
                DisplayName = "TestUser",
                FollowDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("testuser", result["Username"]);
            Assert.Equal("TestUser", result["DisplayName"]);
            Assert.Equal("TestUser", result["User"]);
            Assert.Equal(eventArgs.FollowDate.ToString("o"), result["FollowDate"]);
            Assert.NotNull(result["FollowEventArgs"]);
        }

        [Fact]
        public void ToDictionary_FollowEventArgs_NullStringProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new FollowEventArgs
            {
                UserId = null!,
                Username = null!,
                DisplayName = null!,
                FollowDate = DateTime.UtcNow
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["UserId"]);
            Assert.Equal(string.Empty, result["Username"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
        }

        [Fact]
        public void ToDictionary_CheerEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((CheerEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_CheerEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new CheerEventArgs
            {
                UserId = "12345",
                Name = "testuser",
                DisplayName = "TestUser",
                Message = "cheer message",
                Amount = 100,
                IsAnonymous = true
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("testuser", result["Name"]);
            Assert.Equal("TestUser", result["DisplayName"]);
            Assert.Equal("TestUser", result["User"]);
            Assert.Equal("cheer message", result["Message"]);
            Assert.Equal("100", result["Amount"]);
            Assert.Equal("True", result["IsAnonymous"]);
            Assert.NotNull(result["CheerEventArgs"]);
        }

        [Fact]
        public void ToDictionary_CheerEventArgs_NullProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new CheerEventArgs
            {
                UserId = null,
                Name = null,
                DisplayName = null,
                Message = null,
                Amount = 0,
                IsAnonymous = false
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["UserId"]);
            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
            Assert.Equal(string.Empty, result["Message"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((SubscriptionEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_SubscriptionEventArgs_NullStringProperties_ReturnsEmptyStrings()
        {
            #pragma warning disable CS8625
            var eventArgs = new SubscriptionEventArgs
            {
                UserId = "12345",
                Name = null,
                DisplayName = null,
                Tier = null,
                Count = 5,
                Streak = 3,
                Message = "Thanks for subscribing!"
            };
            #pragma warning restore CS8625

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
            Assert.Equal(string.Empty, result["Tier"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new SubscriptionEventArgs
            {
                UserId = "12345",
                Name = "testuser",
                DisplayName = "TestUser",
                Count = 5,
                Streak = 3,
                Tier = "1000",
                IsGift = true,
                IsRenewal = true,
                HadPreviousSub = false,
                Message = "Thanks for subscribing!"
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("testuser", result["Name"]);
            Assert.Equal("TestUser", result["DisplayName"]);
            Assert.Equal("TestUser", result["User"]);
            Assert.Equal("5", result["Count"]);
            Assert.Equal("3", result["Streak"]);
            Assert.Equal("1000", result["Tier"]);
            Assert.Equal("True", result["IsGift"]);
            Assert.Equal("True", result["IsRenewal"]);
            Assert.Equal("False", result["HadPreviousSub"]);
            Assert.Equal("Thanks for subscribing!", result["Message"]);
            Assert.NotNull(result["SubscriptionEventArgs"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionEventArgs_NullNullableProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new SubscriptionEventArgs
            {
                Name = "testuser",
                UserId = "12345",
                DisplayName = "TestUser",
                Count = null,
                Streak = null,
                Tier = "1000",
                Message = null
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Count"]);
            Assert.Equal(string.Empty, result["Streak"]);
            Assert.Equal(string.Empty, result["Message"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionGiftEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((SubscriptionGiftEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_SubscriptionGiftEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new SubscriptionGiftEventArgs
            {
                UserId = "12345",
                Name = "gifter",
                DisplayName = "Gifter",
                GiftAmount = 5,
                TotalGifted = 50
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("gifter", result["Name"]);
            Assert.Equal("Gifter", result["DisplayName"]);
            Assert.Equal("Gifter", result["User"]);
            Assert.Equal("5", result["GiftAmount"]);
            Assert.Equal("50", result["TotalGifted"]);
            Assert.NotNull(result["SubscriptionGiftEventArgs"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionGiftEventArgs_NullTotalGifted_ReturnsEmptyString()
        {
            var eventArgs = new SubscriptionGiftEventArgs
            {
                UserId = "12345",
                Name = "gifter",
                DisplayName = "Gifter",
                GiftAmount = 5,
                TotalGifted = null
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["TotalGifted"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionGiftEventArgs_NullStringProperties_ReturnsEmptyStrings()
        {
            #pragma warning disable CS8625
            var eventArgs = new SubscriptionGiftEventArgs
            {
                UserId = "12345",
                Name = null,
                DisplayName = null,
                GiftAmount = 5,
                TotalGifted = 50
            };
            #pragma warning restore CS8625

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
        }

        [Fact]
        public void ToDictionary_SubscriptionEndEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((SubscriptionEndEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_SubscriptionEndEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new SubscriptionEndEventArgs
            {
                UserId = "12345",
                Name = "testuser"
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("testuser", result["Name"]);
            Assert.Equal("testuser", result["User"]);
            Assert.NotNull(result["SubscriptionEndEventArgs"]);
        }

        [Fact]
        public void ToDictionary_RaidEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((RaidEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_RaidEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new RaidEventArgs
            {
                UserId = "12345",
                Name = "raider",
                DisplayName = "Raider",
                NumberOfViewers = 25
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("raider", result["Name"]);
            Assert.Equal("Raider", result["DisplayName"]);
            Assert.Equal("Raider", result["User"]);
            Assert.Equal("25", result["NumberOfViewers"]);
            Assert.Equal("25", result["Viewers"]);
            Assert.NotNull(result["RaidEventArgs"]);
        }

        [Fact]
        public void ToDictionary_RaidEventArgs_NullStringProperties_ReturnsEmptyStrings()
        {
            #pragma warning disable CS8625
            var eventArgs = new RaidEventArgs
            {
                UserId = "12345",
                Name = null,
                DisplayName = null,
                NumberOfViewers = 25
            };
            #pragma warning restore CS8625

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
        }

        [Fact]
        public void ToDictionary_ChannelPointRedeemEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((ChannelPointRedeemEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_ChannelPointRedeemEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new ChannelPointRedeemEventArgs
            {
                UserId = "12345",
                Sender = "user1",
                Username = "user2",
                Title = "Reward Title",
                UserInput = "user input value"
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("user1", result["Sender"]);
            Assert.Equal("user1", result["Name"]);
            Assert.Equal("user2", result["Username"]);
            Assert.Equal("user1", result["DisplayName"]);
            Assert.Equal("user1", result["User"]);
            Assert.Equal("Reward Title", result["Title"]);
            Assert.Equal("Reward Title", result["RewardName"]);
            Assert.Equal("user input value", result["UserInput"]);
            Assert.Equal("user input value", result["Message"]);
            Assert.NotNull(result["ChannelPointRedeemEventArgs"]);
        }

        [Fact]
        public void ToDictionary_ChannelPointRedeemEventArgs_NullValues_ReturnsEmptyStrings()
        {
            #pragma warning disable CS8625
            var eventArgs = new ChannelPointRedeemEventArgs
            {
                UserId = null,
                Sender = null,
                Username = null,
                Title = null,
                UserInput = null
            };
            #pragma warning restore CS8625

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["UserId"]);
            Assert.Equal(string.Empty, result["Sender"]);
            Assert.Equal(string.Empty, result["Username"]);
            Assert.Equal(string.Empty, result["Title"]);
            Assert.Equal(string.Empty, result["UserInput"]);
            Assert.Equal(string.Empty, result["Message"]);
        }

        [Fact]
        public void ToDictionary_AdBreakStartEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((AdBreakStartEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_AdBreakStartEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var startedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var eventArgs = new AdBreakStartEventArgs
            {
                Length = 90,
                Automatic = true,
                StartedAt = startedAt
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("90", result["Length"]);
            Assert.Equal("True", result["Automatic"]);
            Assert.Equal(startedAt.ToString("o"), result["StartedAt"]);
            Assert.NotNull(result["AdBreakStartEventArgs"]);
        }

        [Fact]
        public void ToDictionary_BanEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((BanEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_BanEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new BanEventArgs
            {
                UserId = "12345",
                Name = "banneduser",
                IsUnBan = true,
                BanEndsAt = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero)
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("banneduser", result["Name"]);
            Assert.Equal("banneduser", result["User"]);
            Assert.Equal("True", result["IsUnBan"]);
            Assert.NotNull(result["BanEndsAt"]);
            Assert.NotNull(result["BanEventArgs"]);
        }

        [Fact]
        public void ToDictionary_BanEventArgs_NullBanEndsAt_ReturnsEmptyString()
        {
            var eventArgs = new BanEventArgs
            {
                UserId = "12345",
                Name = "banneduser",
                IsUnBan = false,
                BanEndsAt = null
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["BanEndsAt"]);
        }

        [Fact]
        public void ToDictionary_BitsUseEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((BitsUseEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_BitsUseEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new BitsUseEventArgs
            {
                UserId = "12345",
                Name = "bitsuser",
                DisplayName = "BitsUser",
                Amount = 500,
                Message = "bits message",
                Type = "cheer",
                IsPowerUp = true,
                PowerUp = new PowerUp { Type = "gigantify_an_emote" },
                IsCustomPowerUp = true,
                CustomPowerUp = new CustomPowerUp { Title = "Custom Reward", RewardId = "reward-123" },
                HasBitsMessage = true
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("bitsuser", result["Name"]);
            Assert.Equal("BitsUser", result["DisplayName"]);
            Assert.Equal("BitsUser", result["User"]);
            Assert.Equal("500", result["Amount"]);
            Assert.Equal("500", result["Bits"]);
            Assert.Equal("bits message", result["Message"]);
            Assert.Equal("cheer", result["Type"]);
            Assert.Equal("True", result["IsPowerUp"]);
            Assert.Equal("gigantify_an_emote", result["PowerUpType"]);
            Assert.Equal("True", result["IsCustomPowerUp"]);
            Assert.Equal("Custom Reward", result["CustomPowerUpTitle"]);
            Assert.Equal("reward-123", result["CustomPowerUpRewardId"]);
            Assert.Equal("True", result["HasBitsMessage"]);
            Assert.NotNull(result["BitsUseEventArgs"]);
        }

        [Fact]
        public void ToDictionary_BitsUseEventArgs_NullProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new BitsUseEventArgs
            {
                UserId = null,
                Name = null,
                DisplayName = null,
                Amount = 0,
                Message = null,
                Type = "cheer",
                IsPowerUp = false,
                PowerUp = null,
                IsCustomPowerUp = false,
                CustomPowerUp = null,
                HasBitsMessage = false
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["UserId"]);
            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
            Assert.Equal(string.Empty, result["Message"]);
            Assert.Equal(string.Empty, result["PowerUpType"]);
            Assert.Equal(string.Empty, result["CustomPowerUpTitle"]);
            Assert.Equal(string.Empty, result["CustomPowerUpRewardId"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_Null_ReturnsEmptyDictionary()
        {
            var result = TwitchEventArgsConverter.ToDictionary((ChatNotificationEventArgs)null!);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_Populated_ReturnsCorrectDictionary()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                UserId = "12345",
                Name = "notifyuser",
                DisplayName = "NotifyUser",
                IsAnonymous = true,
                NoticeType = "sub",
                SystemMessage = "System message here",
                Message = "User message"
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("notifyuser", result["Name"]);
            Assert.Equal("NotifyUser", result["DisplayName"]);
            Assert.Equal("NotifyUser", result["User"]);
            Assert.Equal("True", result["IsAnonymous"]);
            Assert.Equal("sub", result["NoticeType"]);
            Assert.Equal("System message here", result["SystemMessage"]);
            Assert.Equal("User message", result["Message"]);
            Assert.NotNull(result["ChatNotificationEventArgs"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_NullProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                UserId = null,
                Name = null,
                DisplayName = null,
                IsAnonymous = false,
                NoticeType = "sub",
                SystemMessage = "",
                Message = null
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["UserId"]);
            Assert.Equal(string.Empty, result["Name"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["User"]);
            Assert.Equal(string.Empty, result["Message"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_SubNotice_PopulatesSubFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "sub",
                Sub = new ChatNotificationSubInfo
                {
                    SubTier = "2000",
                    DurationMonths = 2,
                    IsPrime = false
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("2000", result["Sub.SubTier"]);
            Assert.Equal("2", result["Sub.DurationMonths"]);
            Assert.Equal("False", result["Sub.IsPrime"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_ResubNotice_PopulatesResubFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "resub",
                Resub = new ChatNotificationResubInfo
                {
                    CumulativeMonths = 10,
                    DurationMonths = 1,
                    StreakMonths = 3,
                    SubTier = "3000",
                    IsPrime = true,
                    IsGift = true,
                    GifterIsAnonymous = false,
                    GifterUserId = "gifter-123",
                    GifterUserName = "giftername",
                    GifterUserLogin = "gifterlogin"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("10", result["Resub.CumulativeMonths"]);
            Assert.Equal("1", result["Resub.DurationMonths"]);
            Assert.Equal("3", result["Resub.StreakMonths"]);
            Assert.Equal("3000", result["Resub.SubTier"]);
            Assert.Equal("True", result["Resub.IsPrime"]);
            Assert.Equal("True", result["Resub.IsGift"]);
            Assert.Equal("False", result["Resub.GifterIsAnonymous"]);
            Assert.Equal("gifter-123", result["Resub.GifterUserId"]);
            Assert.Equal("giftername", result["Resub.GifterUserName"]);
            Assert.Equal("gifterlogin", result["Resub.GifterUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_SubGiftNotice_PopulatesSubGiftFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "sub_gift",
                SubGift = new ChatNotificationSubGiftInfo
                {
                    DurationMonths = 3,
                    CumulativeTotal = 25,
                    RecipientUserId = "recipient-123",
                    RecipientUserName = "recipientname",
                    RecipientUserLogin = "recipientlogin",
                    SubTier = "1000",
                    CommunityGiftId = "gift-456"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("3", result["SubGift.DurationMonths"]);
            Assert.Equal("25", result["SubGift.CumulativeTotal"]);
            Assert.Equal("recipient-123", result["SubGift.RecipientUserId"]);
            Assert.Equal("recipientname", result["SubGift.RecipientUserName"]);
            Assert.Equal("recipientlogin", result["SubGift.RecipientUserLogin"]);
            Assert.Equal("1000", result["SubGift.SubTier"]);
            Assert.Equal("gift-456", result["SubGift.CommunityGiftId"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_CommunitySubGiftNotice_PopulatesCommunitySubGiftFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "community_sub_gift",
                CommunitySubGift = new ChatNotificationCommunitySubGiftInfo
                {
                    Id = "community-789",
                    Total = 100,
                    SubTier = "1000",
                    CumulativeTotal = 500
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("community-789", result["CommunitySubGift.Id"]);
            Assert.Equal("100", result["CommunitySubGift.Total"]);
            Assert.Equal("1000", result["CommunitySubGift.SubTier"]);
            Assert.Equal("500", result["CommunitySubGift.CumulativeTotal"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_GiftPaidUpgradeNotice_PopulatesGiftPaidUpgradeFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "gift_paid_upgrade",
                GiftPaidUpgrade = new ChatNotificationGiftPaidUpgradeInfo
                {
                    GifterIsAnonymous = true,
                    GifterUserId = "gifter-123",
                    GifterUserName = "giftername",
                    GifterUserLogin = "gifterlogin"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("True", result["GiftPaidUpgrade.GifterIsAnonymous"]);
            Assert.Equal("gifter-123", result["GiftPaidUpgrade.GifterUserId"]);
            Assert.Equal("giftername", result["GiftPaidUpgrade.GifterUserName"]);
            Assert.Equal("gifterlogin", result["GiftPaidUpgrade.GifterUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_PrimePaidUpgradeNotice_PopulatesPrimePaidUpgradeFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "prime_paid_upgrade",
                PrimePaidUpgrade = new ChatNotificationPrimePaidUpgradeInfo
                {
                    SubTier = "1000"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("1000", result["PrimePaidUpgrade.SubTier"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_RaidNotice_PopulatesRaidFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "raid",
                Raid = new ChatNotificationRaidInfo
                {
                    UserId = "raider-123",
                    UserName = "raidername",
                    UserLogin = "raiderlogin",
                    ViewerCount = 50,
                    ProfileImageUrl = "https://example.com/image.png"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("raider-123", result["Raid.UserId"]);
            Assert.Equal("raidername", result["Raid.UserName"]);
            Assert.Equal("raiderlogin", result["Raid.UserLogin"]);
            Assert.Equal("50", result["Raid.ViewerCount"]);
            Assert.Equal("https://example.com/image.png", result["Raid.ProfileImageUrl"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_PayItForwardNotice_PopulatesPayItForwardFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "pay_it_forward",
                PayItForward = new ChatNotificationPayItForwardInfo
                {
                    GifterIsAnonymous = true,
                    GifterUserId = "gifter-123",
                    GifterUserName = "giftername",
                    GifterUserLogin = "gifterlogin",
                    RecipientUserId = "recipient-456",
                    RecipientUserName = "recipientname",
                    RecipientUserLogin = "recipientlogin"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("True", result["PayItForward.GifterIsAnonymous"]);
            Assert.Equal("gifter-123", result["PayItForward.GifterUserId"]);
            Assert.Equal("giftername", result["PayItForward.GifterUserName"]);
            Assert.Equal("gifterlogin", result["PayItForward.GifterUserLogin"]);
            Assert.Equal("recipient-456", result["PayItForward.RecipientUserId"]);
            Assert.Equal("recipientname", result["PayItForward.RecipientUserName"]);
            Assert.Equal("recipientlogin", result["PayItForward.RecipientUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_AnnouncementNotice_PopulatesAnnouncementFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "announcement",
                Announcement = new ChatNotificationAnnouncementInfo
                {
                    Color = "blue"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("blue", result["Announcement.Color"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_CharityDonationNotice_PopulatesCharityDonationFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "charity_donation",
                CharityDonation = new ChatNotificationCharityDonationInfo
                {
                    CharityName = "Charity Org",
                    AmountValue = 550,
                    AmountDecimalPlaces = 2,
                    AmountCurrency = "USD"
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("Charity Org", result["CharityDonation.CharityName"]);
            Assert.Equal("550", result["CharityDonation.AmountValue"]);
            Assert.Equal("2", result["CharityDonation.AmountDecimalPlaces"]);
            Assert.Equal("USD", result["CharityDonation.AmountCurrency"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_BitsBadgeTierNotice_PopulatesBitsBadgeTierFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "bits_badge_tier",
                BitsBadgeTier = new ChatNotificationBitsBadgeTierInfo
                {
                    Tier = 1000
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("1000", result["BitsBadgeTier.Tier"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_WatchStreakNotice_PopulatesWatchStreakFields()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "watch_streak",
                WatchStreak = new ChatNotificationWatchStreakInfo
                {
                    StreakCount = 5,
                    ChannelPointsAwarded = 100
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("5", result["WatchStreak.StreakCount"]);
            Assert.Equal("100", result["WatchStreak.ChannelPointsAwarded"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_ResubNotice_NullableFieldsNull_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "resub",
                Resub = new ChatNotificationResubInfo
                {
                    CumulativeMonths = 10,
                    DurationMonths = 1,
                    StreakMonths = null,
                    SubTier = "3000",
                    IsPrime = false,
                    IsGift = false,
                    GifterIsAnonymous = null,
                    GifterUserId = null,
                    GifterUserName = null,
                    GifterUserLogin = null
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Resub.StreakMonths"]);
            Assert.Equal(string.Empty, result["Resub.GifterIsAnonymous"]);
            Assert.Equal(string.Empty, result["Resub.GifterUserId"]);
            Assert.Equal(string.Empty, result["Resub.GifterUserName"]);
            Assert.Equal(string.Empty, result["Resub.GifterUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_SubGiftNotice_NullableFieldsNull_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "sub_gift",
                SubGift = new ChatNotificationSubGiftInfo
                {
                    DurationMonths = 3,
                    CumulativeTotal = null,
                    RecipientUserId = "recipient-123",
                    RecipientUserName = "recipientname",
                    RecipientUserLogin = "recipientlogin",
                    SubTier = "1000",
                    CommunityGiftId = null
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["SubGift.CumulativeTotal"]);
            Assert.Equal(string.Empty, result["SubGift.CommunityGiftId"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_CommunitySubGiftNotice_NullableFieldsNull_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "community_sub_gift",
                CommunitySubGift = new ChatNotificationCommunitySubGiftInfo
                {
                    Id = "community-789",
                    Total = 100,
                    SubTier = "1000",
                    CumulativeTotal = null
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["CommunitySubGift.CumulativeTotal"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_GiftPaidUpgradeNotice_NullableFieldsNull_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "gift_paid_upgrade",
                GiftPaidUpgrade = new ChatNotificationGiftPaidUpgradeInfo
                {
                    GifterIsAnonymous = false,
                    GifterUserId = null,
                    GifterUserName = null,
                    GifterUserLogin = null
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["GiftPaidUpgrade.GifterUserId"]);
            Assert.Equal(string.Empty, result["GiftPaidUpgrade.GifterUserName"]);
            Assert.Equal(string.Empty, result["GiftPaidUpgrade.GifterUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_PayItForwardNotice_NullableFieldsNull_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "pay_it_forward",
                PayItForward = new ChatNotificationPayItForwardInfo
                {
                    GifterIsAnonymous = false,
                    GifterUserId = null,
                    GifterUserName = null,
                    GifterUserLogin = null,
                    RecipientUserId = null,
                    RecipientUserName = null,
                    RecipientUserLogin = null
                }
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["PayItForward.GifterUserId"]);
            Assert.Equal(string.Empty, result["PayItForward.GifterUserName"]);
            Assert.Equal(string.Empty, result["PayItForward.GifterUserLogin"]);
            Assert.Equal(string.Empty, result["PayItForward.RecipientUserId"]);
            Assert.Equal(string.Empty, result["PayItForward.RecipientUserName"]);
            Assert.Equal(string.Empty, result["PayItForward.RecipientUserLogin"]);
        }

        [Fact]
        public void ToDictionary_ChatNotificationEventArgs_NullNestedProperties_ReturnsEmptyStrings()
        {
            var eventArgs = new ChatNotificationEventArgs
            {
                NoticeType = "sub",
                Sub = null
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal(string.Empty, result["Sub.SubTier"]);
            Assert.Equal(string.Empty, result["Sub.DurationMonths"]);
            Assert.Equal(string.Empty, result["Sub.IsPrime"]);
        }

        [Fact]
        public void StreamOnlineVariables_ReturnsCorrectDictionary()
        {
            var result = TwitchEventArgsConverter.StreamOnlineVariables();

            Assert.Equal("StreamOnline", result["EventType"]);
            Assert.NotNull(result["Timestamp"]);
        }

        [Fact]
        public void StreamOfflineVariables_ReturnsCorrectDictionary()
        {
            var result = TwitchEventArgsConverter.StreamOfflineVariables();

            Assert.Equal("StreamOffline", result["EventType"]);
            Assert.NotNull(result["Timestamp"]);
        }

        [Fact]
        public void ToDictionary_AllMethods_ReturnCaseInsensitiveDictionary()
        {
            var eventArgs = new FollowEventArgs
            {
                UserId = "12345"
            };

            var result = TwitchEventArgsConverter.ToDictionary(eventArgs);

            Assert.Equal("12345", result["userid"]);
            Assert.Equal("12345", result["USERID"]);
            Assert.Equal("12345", result["UserId"]);
        }
    }
}
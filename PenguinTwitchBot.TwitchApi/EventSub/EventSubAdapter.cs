using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.Models;

namespace PenguinTwitchBot.TwitchApi.EventSub;

/// <summary>
/// Adapter to convert TwitchLib EventSub args to TwitchApi payloads.
/// This seam isolates TwitchLib types from app code by exposing only domain event types.
/// </summary>
public static class EventSubAdapter
{
    private static TPayload CreatePayload<TPayload, TEvent>(object metadata, TEvent @event)
        where TPayload : EventSubPayload<TEvent>, new()
        where TEvent : notnull
    {
        return new TPayload
        {
            Metadata = MapMetadata(metadata),
            Event = @event
        };
    }

    /// <summary>
    /// Converts TwitchLib WebSocket metadata to domain EventSubMetadata for deduplication.
    /// </summary>
    public static EventSubMetadata MapMetadata(object metadata)
    {
        if (metadata is WebsocketEventSubMetadata wsMetadata)
        {
            return new EventSubMetadata
            {
                MessageId = wsMetadata.MessageId,
                MessageType = wsMetadata.MessageType,
                MessageTimestamp = wsMetadata.MessageTimestamp
            };
        }
        // Fallback for other metadata types
        return new EventSubMetadata
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = "unknown",
            MessageTimestamp = DateTime.UtcNow
        };
    }
    public static Channel.ChannelChatMessagePayload AdaptChannelChatMessage(ChannelChatMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelChatMessagePayload, Channel.ChannelChatMessage>(args.Metadata, new()
        {
            MessageId = payload.MessageId,
            ChatterUserId = payload.ChatterUserId,
            ChatterUserLogin = payload.ChatterUserLogin,
            ChatterUserName = payload.ChatterUserName,
            IsSubscriber = payload.IsSubscriber,
            IsModerator = payload.IsModerator,
            IsVip = payload.IsVip,
            IsBroadcaster = payload.IsBroadcaster,
            Message = payload.Message.Text,
            Fragments = payload.Message.Fragments.Select(fragment => new Channel.ChannelChatMessageFragment
            {
                Type = fragment.Type,
                Text = fragment.Text,
                Emote = fragment.Emote == null ? null : new Channel.ChannelChatMessageFragmentEmote
                {
                    Id = fragment.Emote.Id,
                    EmoteSetId = fragment.Emote.EmoteSetId,
                    OwnerId = fragment.Emote.OwnerId,
                    Format = fragment.Emote.Format,
                },
                Cheermote = fragment.Cheermote == null ? null : new Channel.ChannelChatMessageFragmentCheermote
                {
                    Prefix = fragment.Cheermote.Prefix,
                    Bits = fragment.Cheermote.Bits,
                },
            }).ToArray(),
            Badges = payload.Badges.Select(b => new Channel.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            }).ToArray(),
            Color = payload.Color,
            ChannelPointsCustomRewardId = payload.ChannelPointsCustomRewardId ?? string.Empty,
            SourceBroadcasterUserId = payload.SourceBroadcasterUserId,
        });
    }

    public static Channel.ChannelFollowPayload AdaptChannelFollow(ChannelFollowArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelFollowPayload, Channel.ChannelFollow>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            FollowedAt = payload.FollowedAt.DateTime,
        });
    }

    public static Channel.ChannelCheerPayload AdaptChannelCheer(ChannelCheerArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelCheerPayload, Channel.ChannelCheer>(args.Metadata, new()
        {
            IsAnonymous = payload.IsAnonymous,
            CheererId = payload.UserId,
            CheererLogin = payload.UserLogin,
            CheererName = payload.UserName,
            Bits = payload.Bits,
            Message = payload.Message,
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
        });
    }

    public static Channel.ChannelRaidPayload AdaptChannelRaid(ChannelRaidArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelRaidPayload, Channel.ChannelRaid>(args.Metadata, new()
        {
            FromBroadcasterUserId = payload.FromBroadcasterUserId,
            FromBroadcasterUserLogin = payload.FromBroadcasterUserLogin,
            FromBroadcasterUserName = payload.FromBroadcasterUserName,
            ToBroadcasterUserId = payload.ToBroadcasterUserId,
            ToBroadcasterUserLogin = payload.ToBroadcasterUserLogin,
            ToBroadcasterUserName = payload.ToBroadcasterUserName,
            Viewers = payload.Viewers,
        });
    }

    public static Channel.ChannelBanPayload AdaptChannelBan(ChannelBanArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelBanPayload, Channel.ChannelBan>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            ModeratorUserId = payload.ModeratorUserId,
            ModeratorUserLogin = payload.ModeratorUserLogin,
            ModeratorUserName = payload.ModeratorUserName,
            IsPermanent = payload.IsPermanent,
            Reason = payload.Reason ?? string.Empty,
            EndsAt = payload.EndsAt,
            BannedAt = payload.BannedAt,
        });
    }

    public static Channel.ChannelUnbanPayload AdaptChannelUnban(ChannelUnbanArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelUnbanPayload, Channel.ChannelUnban>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            ModeratorUserId = payload.ModeratorUserId,
            ModeratorUserLogin = payload.ModeratorUserLogin,
            ModeratorUserName = payload.ModeratorUserName,
        });
    }

    public static Channel.ChannelSubscribePayload AdaptChannelSubscribe(ChannelSubscribeArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelSubscribePayload, Channel.ChannelSubscribe>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Tier = payload.Tier,
            IsGift = payload.IsGift,
        });
    }

    public static Channel.ChannelSubscriptionRenewalPayload AdaptChannelSubscriptionRenewal(ChannelSubscriptionMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelSubscriptionRenewalPayload, Channel.ChannelSubscriptionRenewal>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            CumulativeMonths = payload.CumulativeMonths,
            StreakMonths = payload.StreakMonths ?? 0,
            Tier = payload.Tier,
            Message = payload.Message != null ? new() { Text = payload.Message.Text } : null!,
        });
    }

    public static Channel.ChannelSubscriptionGiftPayload AdaptChannelSubscriptionGift(ChannelSubscriptionGiftArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelSubscriptionGiftPayload, Channel.ChannelSubscriptionGift>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Total = payload.Total,
            CumulativeTotal = payload.CumulativeTotal,
            Tier = payload.Tier,
            IsAnonymous = payload.IsAnonymous,
        });
    }

    public static Channel.ChannelSubscriptionEndPayload AdaptChannelSubscriptionEnd(ChannelSubscriptionEndArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelSubscriptionEndPayload, Channel.ChannelSubscriptionEnd>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
        });
    }

    public static Channel.ChannelPointRedemptionPayload AdaptChannelPointRedemption(ChannelPointsCustomRewardRedemptionArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelPointRedemptionPayload, Channel.ChannelPointRedemption>(args.Metadata, new()
        {
            Id = payload.Id,
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Status = payload.Status,
            UserInput = payload.UserInput ?? string.Empty,
            Reward = new() { Id = payload.Reward.Id, Title = payload.Reward.Title },
            RedeemedAt = payload.RedeemedAt,
        });
    }

    public static Channel.ChannelChatNotificationPayload AdaptChannelChatNotification(ChannelChatNotificationArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelChatNotificationPayload, Channel.ChannelChatNotification>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            MessageId = payload.MessageId,
            ChatterUserId = payload.ChatterUserId,
            ChatterUserLogin = payload.ChatterUserLogin,
            ChatterUserName = payload.ChatterUserName,
            ChatterIsAnonymous = payload.ChatterIsAnonymous,
            Color = payload.Color,
            Badges = payload.Badges.Select(b => new Channel.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            }).ToArray(),
            SystemMessage = payload.SystemMessage,
            NoticeType = payload.NoticeType,
            Message = payload.Message.Text,
            Sub = payload.Sub == null ? null : new Channel.ChatNotificationSubInfo
            {
                SubTier = payload.Sub.SubTier,
                DurationMonths = payload.Sub.DurationMonths,
                IsPrime = payload.Sub.IsPrime,
            },
            Resub = payload.Resub == null ? null : new Channel.ChatNotificationResubInfo
            {
                CumulativeMonths = payload.Resub.CumulativeMonths,
                DurationMonths = payload.Resub.DurationMonths,
                StreakMonths = payload.Resub.StreakMonths,
                SubTier = payload.Resub.SubTier,
                IsPrime = payload.Resub.IsPrime,
                IsGift = payload.Resub.IsGift,
                GifterIsAnonymous = payload.Resub.GifterIsAnonymous,
                GifterUserId = payload.Resub.GifterUserId,
                GifterUserName = payload.Resub.GifterUserName,
                GifterUserLogin = payload.Resub.GifterUserLogin,
            },
            SubGift = payload.SubGift == null ? null : new Channel.ChatNotificationSubGiftInfo
            {
                DurationMonths = payload.SubGift.DurationMonths,
                CumulativeTotal = payload.SubGift.CumulativeTotal,
                RecipientUserId = payload.SubGift.RecipientUserId,
                RecipientUserName = payload.SubGift.RecipientUserName,
                RecipientUserLogin = payload.SubGift.RecipientUserLogin,
                SubTier = payload.SubGift.SubTier,
                CommunityGiftId = payload.SubGift.CommunityGiftId,
            },
            CommunitySubGift = payload.CommunitySubGift == null ? null : new Channel.ChatNotificationCommunitySubGiftInfo
            {
                Id = payload.CommunitySubGift.Id,
                Total = payload.CommunitySubGift.Total,
                SubTier = payload.CommunitySubGift.SubTier,
                CumulativeTotal = payload.CommunitySubGift.CumulativeTotal,
            },
            GiftPaidUpgrade = payload.GiftPaidUpgrade == null ? null : new Channel.ChatNotificationGiftPaidUpgradeInfo
            {
                GifterIsAnonymous = payload.GiftPaidUpgrade.GifterIsAnonymous,
                GifterUserId = payload.GiftPaidUpgrade.GifterUserId,
                GifterUserName = payload.GiftPaidUpgrade.GifterUserName,
                GifterUserLogin = payload.GiftPaidUpgrade.GifterUserLogin,
            },
            PrimePaidUpgrade = payload.PrimePaidUpgrade == null ? null : new Channel.ChatNotificationPrimePaidUpgradeInfo
            {
                SubTier = payload.PrimePaidUpgrade.SubTier,
            },
            Raid = payload.Raid == null ? null : new Channel.ChatNotificationRaidInfo
            {
                UserId = payload.Raid.UserId,
                UserName = payload.Raid.UserName,
                UserLogin = payload.Raid.UserLogin,
                ViewerCount = payload.Raid.ViewerCount,
                ProfileImageUrl = payload.Raid.ProfileImageUrl,
            },
            PayItForward = payload.PayItForward == null ? null : new Channel.ChatNotificationPayItForwardInfo
            {
                GifterIsAnonymous = payload.PayItForward.GifterIsAnonymous,
                GifterUserId = payload.PayItForward.GifterUserId,
                GifterUserName = payload.PayItForward.GifterUserName,
                GifterUserLogin = payload.PayItForward.GifterUserLogin,
                RecipientUserId = payload.PayItForward.RecipientUserId,
                RecipientUserName = payload.PayItForward.RecipientUserName,
                RecipientUserLogin = payload.PayItForward.RecipientUserLogin,
            },
            Announcement = payload.Announcement == null ? null : new Channel.ChatNotificationAnnouncementInfo
            {
                Color = payload.Announcement.Color,
            },
            CharityDonation = payload.CharityDonation == null ? null : new Channel.ChatNotificationCharityDonationInfo
            {
                CharityName = payload.CharityDonation.Name,
                AmountValue = payload.CharityDonation.Amount.Value,
                AmountDecimalPlaces = payload.CharityDonation.Amount.DecimalPlaces,
                AmountCurrency = payload.CharityDonation.Amount.Currency,
            },
            BitsBadgeTier = payload.BitsBadgeTier == null ? null : new Channel.ChatNotificationBitsBadgeTierInfo
            {
                Tier = payload.BitsBadgeTier.Tier,
            },
            WatchStreak = payload.WatchStreak == null ? null : new Channel.ChatNotificationWatchStreakInfo
            {
                StreakCount = payload.WatchStreak.StreakCount,
                ChannelPointsAwarded = payload.WatchStreak.ChannelPointsAwarded,
            },
            SourceBroadcasterUserId = payload.SourceBroadcasterUserId,
            SourceBroadcasterUserName = payload.SourceBroadcasterUserName,
            SourceBroadcasterUserLogin = payload.SourceBroadcasterUserLogin,
            SourceMessageId = payload.SourceMessageId,
            SourceBadges = payload.SourceBadges?.Select(b => new Channel.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            }).ToArray(),
            SharedChatSub = payload.SharedChatSub == null ? null : new Channel.ChatNotificationSubInfo
            {
                SubTier = payload.SharedChatSub.SubTier,
                DurationMonths = payload.SharedChatSub.DurationMonths,
                IsPrime = payload.SharedChatSub.IsPrime,
            },
            IsSourceOnly = payload.IsSourceOnly,
            SharedChatResub = payload.SharedChatResub == null ? null : new Channel.ChatNotificationResubInfo
            {
                CumulativeMonths = payload.SharedChatResub.CumulativeMonths,
                DurationMonths = payload.SharedChatResub.DurationMonths,
                StreakMonths = payload.SharedChatResub.StreakMonths,
                SubTier = payload.SharedChatResub.SubTier,
                IsPrime = payload.SharedChatResub.IsPrime,
                IsGift = payload.SharedChatResub.IsGift,
                GifterIsAnonymous = payload.SharedChatResub.GifterIsAnonymous,
                GifterUserId = payload.SharedChatResub.GifterUserId,
                GifterUserName = payload.SharedChatResub.GifterUserName,
                GifterUserLogin = payload.SharedChatResub.GifterUserLogin,
            },
            SharedChatSubGift = payload.SharedChatSubGift == null ? null : new Channel.ChatNotificationSubGiftInfo
            {
                DurationMonths = payload.SharedChatSubGift.DurationMonths,
                CumulativeTotal = payload.SharedChatSubGift.CumulativeTotal,
                RecipientUserId = payload.SharedChatSubGift.RecipientUserId,
                RecipientUserName = payload.SharedChatSubGift.RecipientUserName,
                RecipientUserLogin = payload.SharedChatSubGift.RecipientUserLogin,
                SubTier = payload.SharedChatSubGift.SubTier,
                CommunityGiftId = payload.SharedChatSubGift.CommunityGiftId,
            },
            SharedChatCommunitySubGift = payload.SharedChatCommunitySubGift == null ? null : new Channel.ChatNotificationCommunitySubGiftInfo
            {
                Id = payload.SharedChatCommunitySubGift.Id,
                Total = payload.SharedChatCommunitySubGift.Total,
                SubTier = payload.SharedChatCommunitySubGift.SubTier,
                CumulativeTotal = payload.SharedChatCommunitySubGift.CumulativeTotal,
            },
            SharedChatGiftPaidUpgrade = payload.SharedChatGiftPaidUpgrade == null ? null : new Channel.ChatNotificationGiftPaidUpgradeInfo
            {
                GifterIsAnonymous = payload.SharedChatGiftPaidUpgrade.GifterIsAnonymous,
                GifterUserId = payload.SharedChatGiftPaidUpgrade.GifterUserId,
                GifterUserName = payload.SharedChatGiftPaidUpgrade.GifterUserName,
                GifterUserLogin = payload.SharedChatGiftPaidUpgrade.GifterUserLogin,
            },
            SharedChatPrimePaidUpgrade = payload.SharedChatPrimePaidUpgrade == null ? null : new Channel.ChatNotificationPrimePaidUpgradeInfo
            {
                SubTier = payload.SharedChatPrimePaidUpgrade.SubTier,
            },
            SharedChatRaid = payload.SharedChatRaid == null ? null : new Channel.ChatNotificationRaidInfo
            {
                UserId = payload.SharedChatRaid.UserId,
                UserName = payload.SharedChatRaid.UserName,
                UserLogin = payload.SharedChatRaid.UserLogin,
                ViewerCount = payload.SharedChatRaid.ViewerCount,
                ProfileImageUrl = payload.SharedChatRaid.ProfileImageUrl,
            },
            SharedChatPayItForward = payload.SharedChatPayItForward == null ? null : new Channel.ChatNotificationPayItForwardInfo
            {
                GifterIsAnonymous = payload.SharedChatPayItForward.GifterIsAnonymous,
                GifterUserId = payload.SharedChatPayItForward.GifterUserId,
                GifterUserName = payload.SharedChatPayItForward.GifterUserName,
                GifterUserLogin = payload.SharedChatPayItForward.GifterUserLogin,
                RecipientUserId = payload.SharedChatPayItForward.RecipientUserId,
                RecipientUserName = payload.SharedChatPayItForward.RecipientUserName,
                RecipientUserLogin = payload.SharedChatPayItForward.RecipientUserLogin,
            },
            SharedChatAnnouncement = payload.SharedChatAnnouncement == null ? null : new Channel.ChatNotificationAnnouncementInfo
            {
                Color = payload.SharedChatAnnouncement.Color,
            },
        });
    }

    public static Channel.ChannelChatMessageDeletePayload AdaptChannelChatMessageDelete(ChannelChatMessageDeleteArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelChatMessageDeletePayload, Channel.ChannelChatMessageDelete>(args.Metadata, new()
        {
            MessageId = payload.MessageId,
            TargetUserName = payload.TargetUserName,
            TargetUserLogin = payload.TargetUserLogin,
            TargetUserId = payload.TargetUserId,
        });
    }

    public static Channel.ChannelSuspiciousUserMessagePayload AdaptChannelSuspiciousUserMessage(ChannelSuspiciousUserMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelSuspiciousUserMessagePayload, Channel.ChannelSuspiciousUserMessage>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Message = new() { MessageId = payload.Message.MessageId, Text = payload.Message.Text },
        });
    }

    public static Channel.ChannelAdBreakBeginPayload AdaptChannelAdBreakBegin(ChannelAdBreakBeginArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelAdBreakBeginPayload, Channel.ChannelAdBreakBegin>(args.Metadata, new()
        {
            DurationSeconds = payload.DurationSeconds,
            IsAutomatic = payload.IsAutomatic,
            StartedAt = payload.StartedAt,
        });
    }

    public static Channel.ChannelBitsUsePayload AdaptChannelBitsUse(ChannelBitsUseArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<Channel.ChannelBitsUsePayload, Channel.ChannelBitsUse>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Bits = payload.Bits,
            Type = payload.Type,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            Message = payload.Message != null ? new()
            {
                Text = payload.Message.Text,
                Fragments = payload.Message.Fragments?.Select(f => new Channel.BitsChatFragment
                {
                    Type = f.Type,
                    Text = f.Text,
                    Emote = f.Emote != null ? new() { Id = f.Emote.Id, EmoteSetId = f.Emote.EmoteSetId, OwnerId = f.Emote.OwnerId, Format = f.Emote.Format } : null,
                }).ToList() ?? [],
            } : null,
            PowerUp = payload.PowerUp != null ? new()
            {
                Type = payload.PowerUp.Type,
                Emote = payload.PowerUp.Emote != null ? new() { Id = payload.PowerUp.Emote.Id } : null,
            } : null,
            CustomPowerUp = payload.CustomPowerUp != null ? new() { Title = payload.CustomPowerUp.Title, RewardId = payload.CustomPowerUp.RewardId } : null,
        });
    }

    public static Stream.StreamOnlinePayload AdaptStreamOnline(StreamOnlineArgs args)
    {
        return CreatePayload<Stream.StreamOnlinePayload, Stream.StreamOnline>(args.Metadata, new());
    }

    public static Stream.StreamOfflinePayload AdaptStreamOffline(StreamOfflineArgs args)
    {
        return CreatePayload<Stream.StreamOfflinePayload, Stream.StreamOffline>(args.Metadata, new());
    }
}


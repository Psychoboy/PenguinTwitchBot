using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.EventArgs.Stream;

namespace PenguinTwitchBot.TwitchApi.EventSub;

/// <summary>
/// Adapter to convert TwitchLib EventSub args to TwitchApi payloads.
/// This seam isolates TwitchLib types from app code by exposing only domain event types.
/// </summary>
public static class EventSubAdapter
{
    private static TPayload CreatePayload<TPayload, TEvent>(object metadata, TEvent @event)
        where TPayload : EventSub.EventArgs.EventSubEventArgs<TEvent>, new()
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
        if (metadata is TwitchLib.EventSub.Websockets.Core.Models.WebsocketEventSubMetadata wsMetadata)
        {
            return new Websockets.Models.WebsocketEventSubMetaData
            {
                MessageId = wsMetadata.MessageId,
                MessageType = wsMetadata.MessageType,
                MessageTimestamp = wsMetadata.MessageTimestamp,
                SubscriptionType = wsMetadata.SubscriptionType,
                SubscriptionVersion = wsMetadata.SubscriptionVersion,
                // HasSubscriptionInfo is derived from SubscriptionType and SubscriptionVersion, so no need to set it explicitly
            };
        }
        // Fallback for other metadata types
        return new Websockets.Models.WebsocketEventSubMetaData
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = "unknown",
            MessageTimestamp = DateTime.UtcNow
        };
    }
    public static EventArgs.Channel.ChannelChatMessageEventArgs AdaptChannelChatMessage(ChannelChatMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelChatMessageEventArgs, SubscriptionTypes.Channel.ChannelChatMessage>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            MessageId = payload.MessageId,
            ChatterUserId = payload.ChatterUserId,
            ChatterUserLogin = payload.ChatterUserLogin,
            ChatterUserName = payload.ChatterUserName,
            Message = payload.Message == null ? new Models.Chat.ChatMessage() : new Models.Chat.ChatMessage
            {
                Text = payload.Message.Text,
                Fragments = payload.Message.Fragments == null ? Array.Empty<Models.Chat.ChatMessageFragment>() : [.. payload.Message.Fragments.Select(f => new Models.Chat.ChatMessageFragment
                {
                    Type = f.Type,
                    Text = f.Text,
                    Emote = f.Emote == null ? null : new Models.Chat.ChatEmote
                    {
                        Id = f.Emote.Id,
                        EmoteSetId = f.Emote.EmoteSetId,
                        OwnerId = f.Emote.OwnerId,
                        Format = f.Emote.Format,
                    },
                    Cheermote = f.Cheermote == null ? null : new Models.Chat.ChatCheermote
                    {
                        Prefix = f.Cheermote.Prefix,
                        Bits = f.Cheermote.Bits,
                    },
                })],
            },
            Color = payload.Color,
            Badges = [.. payload.Badges.Select(b => new Models.Chat.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            })],
            MessageType = payload.MessageType,
            Cheer = payload.Cheer == null ? null : new Models.Chat.ChatCheer
            {
                Bits = payload.Cheer.Bits,
            },
            Reply = payload.Reply == null ? null : new Models.Chat.ChatReply
            {
                ParentMessageId = payload.Reply.ParentMessageId,
                ParentMessageBody = payload.Reply.ParentMessageBody,
                ParentUserId = payload.Reply.ParentUserId,
                ParentUserName = payload.Reply.ParentUserName,
                ParentUserLogin = payload.Reply.ParentUserLogin,
                ThreadMessageId = payload.Reply.ThreadMessageId,
                ThreadUserId = payload.Reply.ThreadUserId,
                ThreadUserName = payload.Reply.ThreadUserName,
                ThreadUserLogin = payload.Reply.ThreadUserLogin,
            },
            ChannelPointsCustomRewardId = payload.ChannelPointsCustomRewardId,
            SourceBroadcasterUserId = payload.SourceBroadcasterUserId,
            SourceBroadcasterUserName = payload.SourceBroadcasterUserName,
            SourceBroadcasterUserLogin = payload.SourceBroadcasterUserLogin,
            SourceMessageId = payload.SourceMessageId,
            SourceBadges = payload.SourceBadges == null ? null : [.. payload.SourceBadges.Select(b => new Models.Chat.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            })],
            IsSourceOnly = payload.IsSourceOnly,
            ChannelPointsAnimationId = payload.ChannelPointsAnimationId,
        });
    }

    public static EventArgs.Channel.ChannelFollowEventArgs AdaptChannelFollow(ChannelFollowArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelFollowEventArgs, SubscriptionTypes.Channel.ChannelFollow>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserName = payload.UserName,
            UserLogin = payload.UserLogin,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            FollowedAt = payload.FollowedAt,
        });
    }

    public static EventArgs.Channel.ChannelCheerEventArgs AdaptChannelCheer(ChannelCheerArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelCheerEventArgs, SubscriptionTypes.Channel.ChannelCheer>(args.Metadata, new()
        {
            IsAnonymous = payload.IsAnonymous,
            Bits = payload.Bits,
            Message = payload.Message,
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
        });
    }

    public static EventArgs.Channel.ChannelRaidEventArgs AdaptChannelRaid(ChannelRaidArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelRaidEventArgs, SubscriptionTypes.Channel.ChannelRaid>(args.Metadata, new()
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

    public static EventArgs.Channel.ChannelBanEventArgs AdaptChannelBan(ChannelBanArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelBanEventArgs, SubscriptionTypes.Channel.ChannelBan>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            ModeratorUserId = payload.ModeratorUserId,
            ModeratorUserLogin = payload.ModeratorUserLogin,
            ModeratorUserName = payload.ModeratorUserName,
            IsPermanent = payload.IsPermanent,
            Reason = payload.Reason,
            EndsAt = payload.EndsAt,
            BannedAt = payload.BannedAt,
        });
    }

    public static EventArgs.Channel.ChannelUnbanEventArgs AdaptChannelUnban(ChannelUnbanArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelUnbanEventArgs, SubscriptionTypes.Channel.ChannelUnban>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            ModeratorUserId = payload.ModeratorUserId,
            ModeratorUserLogin = payload.ModeratorUserLogin,
            ModeratorUserName = payload.ModeratorUserName,
        });
    }

    public static EventArgs.Channel.ChannelSubscribeEventArgs AdaptChannelSubscribe(ChannelSubscribeArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelSubscribeEventArgs, SubscriptionTypes.Channel.ChannelSubscribe>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            Tier = payload.Tier,
            IsGift = payload.IsGift,
        });
    }

    public static EventArgs.Channel.ChannelSubscriptionMessageEventArgs AdaptChannelSubscriptionMessage(ChannelSubscriptionMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelSubscriptionMessageEventArgs, SubscriptionTypes.Channel.ChannelSubscriptionMessage>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            Tier = payload.Tier,
            Message = payload.Message == null ? new Models.Subscriptions.SubscriptionMessage() : new Models.Subscriptions.SubscriptionMessage
            {
                Text = payload.Message.Text,
                Emotes = payload.Message.Emotes == null ? Array.Empty<Models.Subscriptions.SubscriptionMessageEmote>() : [.. payload.Message.Emotes.Select(e => new Models.Subscriptions.SubscriptionMessageEmote
                {
                    Id = e.Id,
                    Begin = e.Begin,
                    End = e.End,
                })],
            },
            CumulativeMonths = payload.CumulativeMonths,
            StreakMonths = payload.StreakMonths,
            DurationMonths = payload.DurationMonths,
        });
    }

    public static EventArgs.Channel.ChannelSubscriptionGiftEventArgs AdaptChannelSubscriptionGift(ChannelSubscriptionGiftArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelSubscriptionGiftEventArgs, SubscriptionTypes.Channel.ChannelSubscriptionGift>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            Total = payload.Total,
            CumulativeTotal = payload.CumulativeTotal,
            Tier = payload.Tier,
            IsAnonymous = payload.IsAnonymous,
        });
    }

    public static EventArgs.Channel.ChannelSubscriptionEndEventArgs AdaptChannelSubscriptionEnd(ChannelSubscriptionEndArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelSubscriptionEndEventArgs, SubscriptionTypes.Channel.ChannelSubscriptionEnd>(args.Metadata, new()
        {
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            Tier = payload.Tier,
            IsGift = payload.IsGift,
        });
    }

    public static EventArgs.Channel.ChannelPointsCustomRewardRedemptionEventArgs AdaptChannelPointRedemption(ChannelPointsCustomRewardRedemptionArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelPointsCustomRewardRedemptionEventArgs, SubscriptionTypes.Channel.ChannelPointsCustomRewardRedemption>(args.Metadata, new()
        {
            Id = payload.Id,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Status = payload.Status,
            UserInput = payload.UserInput ?? string.Empty,
            Reward = new() { 
                Id = payload.Reward.Id, 
                Title = payload.Reward.Title, 
                Cost = payload.Reward.Cost, 
                Prompt = payload.Reward.Prompt 
            },
            RedeemedAt = payload.RedeemedAt,
        });
    }

    public static EventArgs.Channel.ChannelChatNotificationEventArgs AdaptChannelChatNotification(ChannelChatNotificationArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelChatNotificationEventArgs, SubscriptionTypes.Channel.ChannelChatNotification>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            ChatterUserId = payload.ChatterUserId,
            ChatterUserName = payload.ChatterUserName,
            ChatterUserLogin = payload.ChatterUserLogin,
            ChatterIsAnonymous = payload.ChatterIsAnonymous,
            Color = payload.Color,
            Badges = [.. payload.Badges.Select(b => new Models.Chat.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            })],
            SystemMessage = payload.SystemMessage,
            MessageId = payload.MessageId,
            Message = payload.Message == null ? new Models.Chat.ChatMessage() : new Models.Chat.ChatMessage
            {
                Text = payload.Message.Text,
                Fragments = payload.Message.Fragments == null ? Array.Empty<Models.Chat.ChatMessageFragment>() : [.. payload.Message.Fragments.Select(f => new Models.Chat.ChatMessageFragment
                {
                    Type = f.Type,
                    Text = f.Text,
                    Emote = f.Emote == null ? null : new Models.Chat.ChatEmote
                    {
                        Id = f.Emote.Id,
                        EmoteSetId = f.Emote.EmoteSetId,
                        OwnerId = f.Emote.OwnerId,
                        Format = f.Emote.Format,
                    },
                    Cheermote = f.Cheermote == null ? null : new Models.Chat.ChatCheermote
                    {
                        Prefix = f.Cheermote.Prefix,
                        Bits = f.Cheermote.Bits,
                        Tier = f.Cheermote.Tier,
                    },
                    Mention = f.Mention == null ? null : new Models.Chat.ChatMessageMention
                    {
                        UserId = f.Mention.UserId,
                        UserLogin = f.Mention.UserLogin,
                        UserName = f.Mention.UserName,
                    },
                })],
            },
            NoticeType = payload.NoticeType,
            Sub = payload.Sub == null ? null : new Models.Chat.ChatSub
            {
                SubTier = payload.Sub.SubTier,
                DurationMonths = payload.Sub.DurationMonths,
                IsPrime = payload.Sub.IsPrime,
            },
            Resub = payload.Resub == null ? null : new Models.Chat.ChatResub
            {
                SubTier = payload.Resub.SubTier,
                DurationMonths = payload.Resub.DurationMonths,
                CumulativeMonths = payload.Resub.CumulativeMonths,
                StreakMonths = payload.Resub.StreakMonths,
                IsPrime = payload.Resub.IsPrime,
                IsGift = payload.Resub.IsGift,
                GifterIsAnonymous = payload.Resub.GifterIsAnonymous,
                GifterUserId = payload.Resub.GifterUserId,
                GifterUserLogin = payload.Resub.GifterUserLogin,
                GifterUserName = payload.Resub.GifterUserName,
            },
            SubGift = payload.SubGift == null ? null : new Models.Chat.ChatSubGift
            {
                DurationMonths = payload.SubGift.DurationMonths,
                CumulativeTotal = payload.SubGift.CumulativeTotal,
                RecipientUserId = payload.SubGift.RecipientUserId,
                RecipientUserLogin = payload.SubGift.RecipientUserLogin,
                RecipientUserName = payload.SubGift.RecipientUserName,
                SubTier = payload.SubGift.SubTier,
                CommunityGiftId = payload.SubGift.CommunityGiftId,
            },
            CommunitySubGift = payload.CommunitySubGift == null ? null : new Models.Chat.ChatCommunitySubGift
            {
                Id = payload.CommunitySubGift.Id,
                Total = payload.CommunitySubGift.Total,
                SubTier = payload.CommunitySubGift.SubTier,
                CumulativeTotal = payload.CommunitySubGift.CumulativeTotal,
            },
            GiftPaidUpgrade = payload.GiftPaidUpgrade == null ? null : new Models.Chat.ChatGiftPaidUpgrade
            {
                GifterIsAnonymous = payload.GiftPaidUpgrade.GifterIsAnonymous,
                GifterUserId = payload.GiftPaidUpgrade.GifterUserId,
                GifterUserLogin = payload.GiftPaidUpgrade.GifterUserLogin,
                GifterUserName = payload.GiftPaidUpgrade.GifterUserName,
            },
            PrimePaidUpgrade = payload.PrimePaidUpgrade == null ? null : new Models.Chat.ChatPrimePaidUpgrade
            {
                SubTier = payload.PrimePaidUpgrade.SubTier,
            },
            Raid = payload.Raid == null ? null : new Models.Chat.ChatRaid
            {
                UserId = payload.Raid.UserId,
                UserLogin = payload.Raid.UserLogin,
                UserName = payload.Raid.UserName,
                ViewerCount = payload.Raid.ViewerCount,
                ProfileImageUrl = payload.Raid.ProfileImageUrl,
            },
            Unraid = payload.Unraid == null ? null : new Models.Chat.ChatUnraid
            {
                // Nothing extra to map for unraid, but keeping the type for consistency
            },
            PayItForward = payload.PayItForward == null ? null : new Models.Chat.ChatPayItForward
            {
                RecipientUserId = payload.PayItForward.RecipientUserId,
                RecipientUserLogin = payload.PayItForward.RecipientUserLogin,
                RecipientUserName = payload.PayItForward.RecipientUserName,
                GifterIsAnonymous = payload.PayItForward.GifterIsAnonymous,
                GifterUserId = payload.PayItForward.GifterUserId,
                GifterUserLogin = payload.PayItForward.GifterUserLogin,
                GifterUserName = payload.PayItForward.GifterUserName,
            },
            Announcement = payload.Announcement == null ? null : new Models.Chat.ChatAnnouncement
            {
                Color = payload.Announcement.Color,
            },
            CharityDonation = payload.CharityDonation == null ? null : new Models.Chat.ChatCharityDonation
            {
                Name = payload.CharityDonation.Name,
                Amount = payload.CharityDonation.Amount == null ? new Models.Charity.CharityAmount() : new Models.Charity.CharityAmount
                {
                    Value = payload.CharityDonation.Amount.Value,
                    Currency = payload.CharityDonation.Amount.Currency,
                    DecimalPlaces = payload.CharityDonation.Amount.DecimalPlaces,
                },
            },
            BitsBadgeTier = payload.BitsBadgeTier == null ? null : new Models.Chat.ChannelBitsBadgeTier
            {
                Tier = payload.BitsBadgeTier.Tier,
            },
            WatchStreak = payload.WatchStreak == null ? null : new Models.Chat.WatchStreak
            {
                StreakCount = payload.WatchStreak.StreakCount,
                ChannelPointsAwarded = payload.WatchStreak.ChannelPointsAwarded,
            },
            SourceBroadcasterUserId = payload.SourceBroadcasterUserId,
            SourceBroadcasterUserName = payload.SourceBroadcasterUserName,
            SourceBroadcasterUserLogin = payload.SourceBroadcasterUserLogin,
            SourceMessageId = payload.SourceMessageId,
            SourceBadges = payload.SourceBadges == null ? null : [.. payload.SourceBadges.Select(b => new Models.Chat.ChatBadge
            {
                SetId = b.SetId,
                Id = b.Id,
                Info = b.Info,
            })],
            IsSourceOnly = payload.IsSourceOnly,
            SharedChatSub = payload.SharedChatSub == null ? null : new Models.Chat.ChatSub
            {
                SubTier = payload.SharedChatSub.SubTier,
                DurationMonths = payload.SharedChatSub.DurationMonths,
                IsPrime = payload.SharedChatSub.IsPrime,
            },
            SharedChatResub = payload.SharedChatResub == null ? null : new Models.Chat.ChatResub
            {
                SubTier = payload.SharedChatResub.SubTier,
                DurationMonths = payload.SharedChatResub.DurationMonths,
                CumulativeMonths = payload.SharedChatResub.CumulativeMonths,
                StreakMonths = payload.SharedChatResub.StreakMonths,
                IsPrime = payload.SharedChatResub.IsPrime,
                IsGift = payload.SharedChatResub.IsGift,
                GifterIsAnonymous = payload.SharedChatResub.GifterIsAnonymous,
                GifterUserId = payload.SharedChatResub.GifterUserId,
                GifterUserLogin = payload.SharedChatResub.GifterUserLogin,
                GifterUserName = payload.SharedChatResub.GifterUserName,
            },
            SharedChatSubGift = payload.SharedChatSubGift == null ? null : new Models.Chat.ChatSubGift
            {
                DurationMonths = payload.SharedChatSubGift.DurationMonths,
                CumulativeTotal = payload.SharedChatSubGift.CumulativeTotal,
                RecipientUserId = payload.SharedChatSubGift.RecipientUserId,
                RecipientUserLogin = payload.SharedChatSubGift.RecipientUserLogin,
                RecipientUserName = payload.SharedChatSubGift.RecipientUserName,
                SubTier = payload.SharedChatSubGift.SubTier,
                CommunityGiftId = payload.SharedChatSubGift.CommunityGiftId,
            },
            SharedChatCommunitySubGift = payload.SharedChatCommunitySubGift == null ? null : new Models.Chat.ChatCommunitySubGift
            {
                Id = payload.SharedChatCommunitySubGift.Id,
                Total = payload.SharedChatCommunitySubGift.Total,
                SubTier = payload.SharedChatCommunitySubGift.SubTier,
                CumulativeTotal = payload.SharedChatCommunitySubGift.CumulativeTotal,
            },
            SharedChatGiftPaidUpgrade = payload.SharedChatGiftPaidUpgrade == null ? null : new Models.Chat.ChatGiftPaidUpgrade
            {
                GifterIsAnonymous = payload.SharedChatGiftPaidUpgrade.GifterIsAnonymous,
                GifterUserId = payload.SharedChatGiftPaidUpgrade.GifterUserId,
                GifterUserLogin = payload.SharedChatGiftPaidUpgrade.GifterUserLogin,
                GifterUserName = payload.SharedChatGiftPaidUpgrade.GifterUserName,
            },
            SharedChatPrimePaidUpgrade = payload.SharedChatPrimePaidUpgrade == null ? null : new Models.Chat.ChatPrimePaidUpgrade
            {
                SubTier = payload.SharedChatPrimePaidUpgrade.SubTier,
            },
            SharedChatRaid = payload.SharedChatRaid == null ? null : new Models.Chat.ChatRaid
            {
                UserId = payload.SharedChatRaid.UserId,
                UserLogin = payload.SharedChatRaid.UserLogin,
                UserName = payload.SharedChatRaid.UserName,
                ViewerCount = payload.SharedChatRaid.ViewerCount,
                ProfileImageUrl = payload.SharedChatRaid.ProfileImageUrl,
            },
            SharedChatPayItForward = payload.SharedChatPayItForward == null ? null : new Models.Chat.ChatPayItForward
            {
                RecipientUserId = payload.SharedChatPayItForward.RecipientUserId,
                RecipientUserLogin = payload.SharedChatPayItForward.RecipientUserLogin,
                RecipientUserName = payload.SharedChatPayItForward.RecipientUserName,
                GifterIsAnonymous = payload.SharedChatPayItForward.GifterIsAnonymous,
                GifterUserId = payload.SharedChatPayItForward.GifterUserId,
                GifterUserLogin = payload.SharedChatPayItForward.GifterUserLogin,
                GifterUserName = payload.SharedChatPayItForward.GifterUserName,
            },
            SharedChatAnnouncement = payload.SharedChatAnnouncement == null ? null : new Models.Chat.ChatAnnouncement
            {
                Color = payload.SharedChatAnnouncement.Color,
            },
        });
    }

    public static EventArgs.Channel.ChannelChatMessageDeleteEventArgs AdaptChannelChatMessageDelete(ChannelChatMessageDeleteArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelChatMessageDeleteEventArgs, SubscriptionTypes.Channel.ChannelChatMessageDelete>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            TargetUserId = payload.TargetUserId,
            TargetUserName = payload.TargetUserName,
            TargetUserLogin = payload.TargetUserLogin,
            MessageId = payload.MessageId,
        });
    }

    public static EventArgs.Channel.ChannelSuspiciousUserMessageEventArgs AdaptChannelSuspiciousUserMessage(ChannelSuspiciousUserMessageArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelSuspiciousUserMessageEventArgs, SubscriptionTypes.Channel.ChannelSuspiciousUserMessage>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserName = payload.BroadcasterUserName,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            UserId = payload.UserId,
            UserName = payload.UserName,
            UserLogin = payload.UserLogin,
            LowTrustStatus = payload.LowTrustStatus,
            SharedBanChannelIds = payload.SharedBanChannelIds,
            Types = payload.Types,
            BanEvasionEvaluation = payload.BanEvasionEvaluation,
            Message = new Models.ChannelSuspiciousUser.SuspiciousUserMessage
            {
                MessageId = payload.Message.MessageId,
                Text = payload.Message.Text,
                Fragments = payload.Message.Fragments == null ? Array.Empty<Models.ChannelSuspiciousUser.MessageFragment>()
                : [.. payload.Message.Fragments.Select(f => new Models.ChannelSuspiciousUser.MessageFragment
                {
                    Type = f.Type,
                    Text = f.Text,
                    Cheermote = f.Cheermote == null ? null : new Models.ChannelSuspiciousUser.FragmentCheermote
                    {
                       Prefix = f.Cheermote.Prefix,
                       Bits = f.Cheermote.Bits,
                       Tier = f.Cheermote.Tier,
                    },
                    Emote = f.Emote == null ? null : new Models.ChannelSuspiciousUser.FragmentEmote
                    {
                        Id = f.Emote.Id,
                        EmoteSetId = f.Emote.EmoteSetId,
                    },
                })],
            },
        });
    }

    public static EventArgs.Channel.ChannelAdBreakBeginEventArgs AdaptChannelAdBreakBegin(ChannelAdBreakBeginArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelAdBreakBeginEventArgs, SubscriptionTypes.Channel.ChannelAdBreakBegin>(args.Metadata, new()
        {
            DurationSeconds = payload.DurationSeconds,
            StartedAt = payload.StartedAt,
            IsAutomatic = payload.IsAutomatic,
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            RequesterUserId = payload.RequesterUserId,
            RequesterUserLogin = payload.RequesterUserLogin,
            RequesterUserName = payload.RequesterUserName,
        });
    }

    public static EventArgs.Channel.ChannelBitsUseEventArgs AdaptChannelBitsUse(ChannelBitsUseArgs args)
    {
        var payload = args.Payload.Event;
        return CreatePayload<EventArgs.Channel.ChannelBitsUseEventArgs, SubscriptionTypes.Channel.ChannelBitsUse>(args.Metadata, new()
        {
            BroadcasterUserId = payload.BroadcasterUserId,
            BroadcasterUserLogin = payload.BroadcasterUserLogin,
            BroadcasterUserName = payload.BroadcasterUserName,
            UserId = payload.UserId,
            UserLogin = payload.UserLogin,
            UserName = payload.UserName,
            Bits = payload.Bits,
            Type = payload.Type,
            Message = payload.Message == null ? null : new Models.Bits.BitsMessage
            {
               Text = payload.Message.Text,
               Fragments = payload.Message.Fragments == null ? [] : [.. payload.Message.Fragments.Select(f => new Models.Bits.BitsMessageFragments
               {
                   Text = f.Text,
                   Type = f.Type,
                   Emote = f.Emote == null ? null : new Models.Bits.BitsEmote
                   {
                       Id = f.Emote.Id,
                       EmoteSetId = f.Emote.EmoteSetId,
                       OwnerId = f.Emote.OwnerId,
                       Format = f.Emote.Format,
                   },
                   Cheermote = f.Cheermote == null ? null : new Models.Bits.BitsCheermote
                   {
                       Prefix = f.Cheermote.Prefix,
                       Bits = f.Cheermote.Bits,
                       Tier = f.Cheermote.Tier,
                   },
               })],
            },
            PowerUp = payload.PowerUp == null ? null : new Models.Bits.PowerUp
            {
                Type = payload.PowerUp.Type,
                MessageEffectId = payload.PowerUp.MessageEffectId,
                Emote = payload.PowerUp.Emote == null ? null : new Models.Bits.PowerUpEmote
                {
                    Id = payload.PowerUp.Emote.Id,
                    Name = payload.PowerUp.Emote.Name,
                },
            },
            CustomPowerUp = payload.CustomPowerUp == null ? null : new Models.Bits.CustomPowerUp
            {
                Title = payload.CustomPowerUp.Title,
                RewardId = payload.CustomPowerUp.RewardId,
            },
        });
    }

    public static EventArgs.Stream.StreamOnlineEventArgs AdaptStreamOnline(StreamOnlineArgs args)
    {
        return CreatePayload<EventArgs.Stream.StreamOnlineEventArgs, SubscriptionTypes.Stream.StreamOnline>(args.Metadata, new());
    }

    public static EventArgs.Stream.StreamOfflineEventArgs AdaptStreamOffline(StreamOfflineArgs args)
    {
        return CreatePayload<EventArgs.Stream.StreamOfflineEventArgs, SubscriptionTypes.Stream.StreamOffline>(args.Metadata, new());
    }
}


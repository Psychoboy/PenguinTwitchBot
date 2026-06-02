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
            MessageId = payload.MessageId,
            ChatterUserId = payload.ChatterUserId,
            ChatterUserLogin = payload.ChatterUserLogin,
            ChatterUserName = payload.ChatterUserName,
            NoticeType = payload.NoticeType,
            Message = payload.Message.Text,
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


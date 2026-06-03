using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
using PenguinTwitchBot.Bot.Services.Chat;
using PenguinTwitchBot.TwitchApi.EventSub;
using EventSubChannel = PenguinTwitchBot.TwitchApi.EventSub.Channel;
using EventSubStream = PenguinTwitchBot.TwitchApi.EventSub.Stream;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public partial class TwitchWebsocketHostedService(
        ILogger<TwitchWebsocketHostedService> logger,
        IServiceBackbone eventService,
        EventSubWebsocketClient eventSubWebsocketClient,
        SubscriptionTracker subscriptionHistory,
        ChatMessageIdTracker messageIdTracker,
        IMemoryCache memoryCache,
        ITwitchService twitchService,
        TimeProvider timeProvider,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ITwitchEventActionHandler twitchEventActionHandler,
        IChatColorService chatColorService) : ITwitchWebsocketHostedService
    {
        private readonly ConcurrentDictionary<string, DateTime> SubCache = new();
        static readonly SemaphoreSlim _subscriptionLock = new(1);
        private volatile bool Reconnecting = false;
        private TimeSpan KeepAliveTimer = TimeSpan.MinValue;
        private ITimer? JoinTimer;
        private DateTimeOffset LastMessageReceived = DateTimeOffset.MinValue;

        private async Task ChannelChatMessage(object? sender, ChannelChatMessageArgs args)
        {
            await ChannelChatMessage(EventSubAdapter.AdaptChannelChatMessage(args));
        }

        private async Task ChannelChatMessage(EventSubChannel.ChannelChatMessagePayload payload)
        {
            if (messageIdTracker.IsSelfMessage(payload.Event.MessageId)) return;
            if (DidProcessMessage(payload.Metadata)) return;

            var messageText = payload.Event.Message;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();

            if (string.IsNullOrEmpty(payload.Event.ChannelPointsCustomRewardId) == false)
            {
                var channelPoint = await twitchService.GetCustomReward(payload.Event.ChannelPointsCustomRewardId);
                if (channelPoint == null)
                {
                    logger.LogError("Failed to get channel point");
                    return;
                }

                var channelPointRedeemEventArgs = new ChannelPointRedeemEventArgs
                {
                    UserId = payload.Event.ChatterUserId,
                    Sender = payload.Event.ChatterUserName,
                    Username = payload.Event.ChatterUserLogin,
                    Title = channelPoint.Title,
                    UserInput = messageText
                };

                var pointRedemption = new EventSubChannel.ChannelPointRedemption
                {
                    Id = "chat-msg-" + payload.Event.MessageId,
                    UserId = payload.Event.ChatterUserId,
                    UserLogin = payload.Event.ChatterUserLogin,
                    UserName = payload.Event.ChatterUserName,
                    UserInput = messageText,
                    Reward = new EventSubChannel.ChannelPointReward
                    {
                        Id = payload.Event.ChannelPointsCustomRewardId,
                        Title = channelPoint.Title
                    },
                    Status = "fulfilled"
                };

                await eventService.OnChannelPointRedeem(
                    payload.Event.ChatterUserId,
                   payload.Event.ChatterUserName.ToLower(),
                   channelPoint.Title,
                   messageText);
                await twitchEventActionHandler.HandleChannelPointRedemptionAsync(channelPointRedeemEventArgs);
                logger.LogInformation("Channel pointed redeemed: {Title} by {user} userInput: {userInput}", channelPoint.Title, payload.Event.ChatterUserName, messageText);
            }
            else
            {
                logger.LogInformation("CHATMSG: {name}: {message}", payload.Event.ChatterUserName, messageText);
                await Task.WhenAll([ProcessCommandMessage(payload.Event), ProcessChatMessage(payload.Event)]);
            }
        }

        
        private async Task OnChannelChatNotification(object? sender, ChannelChatNotificationArgs args)
        {
            await OnChannelChatNotification(EventSubAdapter.AdaptChannelChatNotification(args));
        }

        private async Task OnChannelChatNotification(EventSubChannel.ChannelChatNotificationPayload payload)
        {
            if (messageIdTracker.IsSelfMessage(payload.Event.MessageId)) return;
            if (DidProcessMessage(payload.Metadata)) return;

            try
            {
                logger.LogInformation("ChatNotification: {NoticeType} from {User}", payload.Event.NoticeType, payload.Event.ChatterUserName);
                var e = payload.Event;
                var eventArgs = new Events.ChatNotificationEventArgs
                {
                    UserId = e.ChatterUserId,
                    Name = e.ChatterUserLogin,
                    DisplayName = e.ChatterUserName,
                    IsAnonymous = e.ChatterIsAnonymous,
                    NoticeType = e.NoticeType,
                    SystemMessage = e.SystemMessage,
                    Message = e.Message?.Text,
                    Sub = e.Sub == null ? null : new Events.ChatNotificationSubInfo
                    {
                        SubTier = e.Sub.SubTier,
                        DurationMonths = e.Sub.DurationMonths,
                        IsPrime = e.Sub.IsPrime,
                    },
                    Resub = e.Resub == null ? null : new Events.ChatNotificationResubInfo
                    {
                        CumulativeMonths = e.Resub.CumulativeMonths,
                        DurationMonths = e.Resub.DurationMonths,
                        StreakMonths = e.Resub.StreakMonths,
                        SubTier = e.Resub.SubTier,
                        IsPrime = e.Resub.IsPrime,
                        IsGift = e.Resub.IsGift,
                        GifterIsAnonymous = e.Resub.GifterIsAnonymous,
                        GifterUserId = e.Resub.GifterUserId,
                        GifterUserName = e.Resub.GifterUserName,
                        GifterUserLogin = e.Resub.GifterUserLogin,
                    },
                    SubGift = e.SubGift == null ? null : new Events.ChatNotificationSubGiftInfo
                    {
                        DurationMonths = e.SubGift.DurationMonths,
                        CumulativeTotal = e.SubGift.CumulativeTotal,
                        RecipientUserId = e.SubGift.RecipientUserId,
                        RecipientUserName = e.SubGift.RecipientUserName,
                        RecipientUserLogin = e.SubGift.RecipientUserLogin,
                        SubTier = e.SubGift.SubTier,
                        CommunityGiftId = e.SubGift.CommunityGiftId,
                    },
                    CommunitySubGift = e.CommunitySubGift == null ? null : new Events.ChatNotificationCommunitySubGiftInfo
                    {
                        Id = e.CommunitySubGift.Id,
                        Total = e.CommunitySubGift.Total,
                        SubTier = e.CommunitySubGift.SubTier,
                        CumulativeTotal = e.CommunitySubGift.CumulativeTotal,
                    },
                    GiftPaidUpgrade = e.GiftPaidUpgrade == null ? null : new Events.ChatNotificationGiftPaidUpgradeInfo
                    {
                        GifterIsAnonymous = e.GiftPaidUpgrade.GifterIsAnonymous,
                        GifterUserId = e.GiftPaidUpgrade.GifterUserId,
                        GifterUserName = e.GiftPaidUpgrade.GifterUserName,
                        GifterUserLogin = e.GiftPaidUpgrade.GifterUserLogin,
                    },
                    PrimePaidUpgrade = e.PrimePaidUpgrade == null ? null : new Events.ChatNotificationPrimePaidUpgradeInfo
                    {
                        SubTier = e.PrimePaidUpgrade.SubTier,
                    },
                    Raid = e.Raid == null ? null : new Events.ChatNotificationRaidInfo
                    {
                        UserId = e.Raid.UserId,
                        UserName = e.Raid.UserName,
                        UserLogin = e.Raid.UserLogin,
                        ViewerCount = e.Raid.ViewerCount,
                        ProfileImageUrl = e.Raid.ProfileImageUrl,
                    },
                    PayItForward = e.PayItForward == null ? null : new Events.ChatNotificationPayItForwardInfo
                    {
                        GifterIsAnonymous = e.PayItForward.GifterIsAnonymous,
                        GifterUserId = e.PayItForward.GifterUserId,
                        GifterUserName = e.PayItForward.GifterUserName,
                        GifterUserLogin = e.PayItForward.GifterUserLogin,
                        RecipientUserId = e.PayItForward.RecipientUserId,
                        RecipientUserName = e.PayItForward.RecipientUserName,
                        RecipientUserLogin = e.PayItForward.RecipientUserLogin,
                    },
                    Announcement = e.Announcement == null ? null : new Events.ChatNotificationAnnouncementInfo
                    {
                        Color = e.Announcement.Color,
                    },
                    // CharityDonation = e.CharityDonation == null ? null : new Events.ChatNotificationCharityDonationInfo
                    // {
                    //     CharityName = e.CharityDonation.Name,
                    //     AmountValue = e.CharityDonation.Amount.Value,
                    //     AmountDecimalPlaces = e.CharityDonation.Amount.DecimalPlaces,
                    //     AmountCurrency = e.CharityDonation.Amount.Currency,
                    // },
                    BitsBadgeTier = e.BitsBadgeTier == null ? null : new Events.ChatNotificationBitsBadgeTierInfo
                    {
                        Tier = e.BitsBadgeTier.Tier,
                    },
                    WatchStreak = e.WatchStreak == null ? null : new Events.ChatNotificationWatchStreakInfo
                    {
                        StreakCount = e.WatchStreak.StreakCount,
                        ChannelPointsAwarded = e.WatchStreak.ChannelPointsAwarded,
                    },
                };

                await twitchEventActionHandler.HandleChatNotificationAsync(eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing chat notification");
            }
        }

        private Task ChannelChatMessageDelete(object? sender, ChannelChatMessageDeleteArgs args)
        {
            logger.LogInformation("CHATMSG DELETE: MessageId: {messageId} User: {userName}", args.Payload.Event.MessageId, args.Payload.Event.TargetUserName);
            return dispatcher.Publish(new DeletedChatMessage { EventArgs = args });
        }

        private Task ChannelSuspiciousUserMessage(object? sender, ChannelSuspiciousUserMessageArgs args)
        {
            if (messageIdTracker.IsSelfMessage(args.Payload.Event.Message.MessageId)) return Task.CompletedTask;
            if (DidProcessMessage(EventSubAdapter.MapMetadata(args.Metadata))) return Task.CompletedTask;
            logger.LogInformation("SUSPICIOUS CHAT: {name}: {message}", args.Payload.Event.UserName, args.Payload.Event.Message);
            var e = args.Payload.Event;
            var messageText = args.Payload.Event.Message.Text;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();
            var chatMessage = new ChatMessageEventArgs
            {
                Message = messageText,
                UserId = e.UserId,
                Name = e.UserLogin,
                DisplayName = e.UserName,
                IsSub = false,
                IsMod = false,
                IsVip = false,
                IsBroadcaster = false,
            };
            return dispatcher.Publish(new ReceivedChatMessage { EventArgs = chatMessage });
        }

        private Task ProcessChatMessage(EventSubChannel.ChannelChatMessage e)
        {
            var messageText = e.Message;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();

            var fragments = (e.Fragments ?? [])
                .Select(f => MapFragment(f))
                .ToList();

            var badges = (e.Badges ?? [])
                .Select(b => new ChatOverlayBadge { SetId = b.SetId, Id = b.Id })
                .ToList();

            var chatMessage = new ChatMessageEventArgs
            {
                Message = messageText,
                UserId = e.ChatterUserId,
                Name = e.ChatterUserLogin.ToLower(),
                DisplayName = e.ChatterUserName,
                IsSub = e.IsSubscriber,
                IsMod = e.IsModerator,
                IsVip = e.IsVip,
                IsBroadcaster = e.IsBroadcaster,
                MessageId = e.MessageId,
                FromOwnChannel = string.IsNullOrWhiteSpace(e.SourceBroadcasterUserId),
                Fragments = fragments,
                Badges = badges,
                ResolvedColor = chatColorService.GetOrAssignColor(e.ChatterUserId, e.Color),
            };
            return dispatcher.Publish(new ReceivedChatMessage { EventArgs = chatMessage });
        }

        private static ChatOverlayFragment MapFragment(EventSubChannel.ChannelChatMessageFragment f)
        {
            if (f.Type == "emote" && f.Emote != null)
            {
                var format = (f.Emote.Format?.Contains("animated") == true) ? "animated" : "static";
                var url = $"https://static-cdn.jtvnw.net/emoticons/v2/{f.Emote.Id}/{format}/dark/1.0";
                return new ChatOverlayFragment
                {
                    Type = "emote",
                    Text = f.Text,
                    EmoteId = f.Emote.Id,
                    EmoteProvider = "twitch",
                    EmoteUrl = url,
                };
            }

            if (f.Type == "cheermote" && f.Cheermote != null)
            {
                return new ChatOverlayFragment
                {
                    Type = "cheermote",
                    Text = f.Text,
                    EmoteId = f.Cheermote.Prefix,
                    EmoteProvider = "twitch",
                    CheerAmount = f.Cheermote.Bits,
                };
            }

            return new ChatOverlayFragment
            {
                Type = f.Type ?? "text",
                Text = f.Text,
            };
        }

        private async Task ProcessCommandMessage(EventSubChannel.ChannelChatMessage e)
        {
            if (e.Message.StartsWith('!') == false) return;
            var messageText = e.Message;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();
            var argsFull = messageText.Split(' ', 2);
            var command = argsFull[0];
            var ArgumentsAsString = argsFull.Length > 1 ? argsFull[1] : "";
            var ArgumentsAsList = string.IsNullOrWhiteSpace(ArgumentsAsString) ? [] : ArgumentsAsString.Split(" ").ToList();
            var eventArgs = new CommandEventArgs
            {
                Command = command[1..].ToLower(),
                Arg = ArgumentsAsString,
                Args = ArgumentsAsList,
                IsWhisper = false,
                UserId = e.ChatterUserId,
                Name = e.ChatterUserLogin,
                DisplayName = e.ChatterUserName,
                IsSub = e.IsSubscriber,
                IsMod = e.IsModerator,
                IsVip = e.IsVip,
                IsBroadcaster = e.IsBroadcaster,
                TargetUser = ArgumentsAsList.Count > 0 ? ArgumentsAsList[0].Replace("@", "").Trim() : "",
                FromOwnChannel = string.IsNullOrWhiteSpace(e.SourceBroadcasterUserId),
                MessageId = e.MessageId
            };
            await eventService.OnCommand(eventArgs);
        }

        public async Task AdBreak(AdBreakStartEventArgs e)
        {
            await eventService.OnAdBreakStartEvent(e);
        }

        private async Task ChannelAdBreakBegin(object? sender, ChannelAdBreakBeginArgs e)
        {
            await ChannelAdBreakBegin(EventSubAdapter.AdaptChannelAdBreakBegin(e));
        }

        private async Task ChannelAdBreakBegin(EventSubChannel.ChannelAdBreakBeginPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("Ad Begin. Length: {length} Started At: {startedAt} Automatic: {automatic}", payload.Event.DurationSeconds, payload.Event.StartedAt, payload.Event.IsAutomatic);
                var ev = new AdBreakStartEventArgs
                {
                    Automatic = payload.Event.IsAutomatic,
                    Length = payload.Event.DurationSeconds,
                    StartedAt = payload.Event.StartedAt
                };
                await twitchEventActionHandler.HandleAdBreakBeginAsync(ev);
                await AdBreak(ev);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelUnBan(object? sender, ChannelUnbanArgs e)
        {
            await OnChannelUnBan(EventSubAdapter.AdaptChannelUnban(e));
        }

        private async Task OnChannelUnBan(EventSubChannel.ChannelUnbanPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("OnChannelUnBan {UserLogin}", payload.Event.UserLogin);
                await eventService.OnViewerBan(payload.Event.UserId, payload.Event.UserLogin, true, null);
                var eventArgs = new BanEventArgs
                {
                    UserId = payload.Event.UserId,
                    Name = payload.Event.UserLogin,
                    DisplayName = payload.Event.UserName,
                    UserName = payload.Event.UserName,
                    UserLogin = payload.Event.UserLogin,
                    ModeratorUserId = payload.Event.ModeratorUserId,
                    ModeratorLogin = payload.Event.ModeratorUserLogin,
                    ModeratorUserName = payload.Event.ModeratorUserName,
                    IsPermanent = false,
                    IsUnBan = true
                };
                await twitchEventActionHandler.HandleChannelUnbanAsync(eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelBan(object? sender, ChannelBanArgs e)
        {
            await OnChannelBan(EventSubAdapter.AdaptChannelBan(e));
        }

        private async Task OnChannelBan(EventSubChannel.ChannelBanPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                if (payload.Event.IsPermanent == false)
                {
                    logger.LogInformation("{UserLogin} timed out by {Moderator}.", payload.Event.UserLogin, payload.Event.ModeratorUserLogin);
                    await dispatcher.Publish(new BannedChatUser { UserId = payload.Event.UserId });
                    return;
                }
                logger.LogInformation("{UserLogin} banned by {Moderator}", payload.Event.UserLogin, payload.Event.ModeratorUserLogin);
                await dispatcher.Publish(new BannedChatUser { UserId = payload.Event.UserId });
                
                await eventService.OnViewerBan(payload.Event.UserId, payload.Event.UserLogin, false, payload.Event.EndsAt);
                var eventArgs = new BanEventArgs
                {
                    UserId = payload.Event.UserId,
                    Name = payload.Event.UserLogin,
                    DisplayName = payload.Event.UserName,
                    UserName = payload.Event.UserName,
                    UserLogin = payload.Event.UserLogin,
                    ModeratorUserId = payload.Event.ModeratorUserId,
                    ModeratorLogin = payload.Event.ModeratorUserLogin,
                    ModeratorUserName = payload.Event.ModeratorUserName,
                    Reason = payload.Event.Reason,
                    BannedAt = payload.Event.BannedAt,
                    IsPermanent = payload.Event.IsPermanent,
                    IsUnBan = false,
                    BanEndsAt = payload.Event.EndsAt
                };
                await twitchEventActionHandler.HandleChannelBanAsync(eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelRaid(object? sender, ChannelRaidArgs e)
        {
            await OnChannelRaid(EventSubAdapter.AdaptChannelRaid(e));
        }

        private async Task OnChannelRaid(EventSubChannel.ChannelRaidPayload payload)
        {
            try
            {
                if (DidProcessMessage(payload.Metadata)) return;

                logger.LogInformation("OnChannelRaid from {BroadcasterName}", payload.Event.FromBroadcasterUserName);

                var raidEventArgs = new Events.RaidEventArgs
                {
                    Name = payload.Event.FromBroadcasterUserLogin,
                    UserId = payload.Event.FromBroadcasterUserId,
                    DisplayName = payload.Event.FromBroadcasterUserName,
                    NumberOfViewers = payload.Event.Viewers
                };

                await eventService.OnIncomingRaid(raidEventArgs);
                await twitchEventActionHandler.HandleRaidAsync(raidEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private bool DidProcessMessage(PenguinTwitchBot.TwitchApi.EventSub.EventSubMetadata eventSubMetadata)
        {
            if (memoryCache.TryGetValue(eventSubMetadata.MessageId, out var _))
            {
                logger.LogWarning("Already processed message: {MessageId} - {MessageType} - {MessageTimestamp}", eventSubMetadata.MessageId, eventSubMetadata.MessageType, eventSubMetadata.MessageTimestamp);
                return true;
            }

            memoryCache.Set(eventSubMetadata.MessageId, eventSubMetadata.MessageId, TimeSpan.FromMinutes(10));
            return false;
        }

        public async Task StreamOffline()
        {
            try
            {
                logger.LogInformation("Stream is offline");
                eventService.IsOnline = false;
                await eventService.OnStreamEnded();
                await twitchEventActionHandler.HandleStreamOfflineAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnStreamOffline(object? sender, StreamOfflineArgs e)
        {
            await OnStreamOffline(EventSubAdapter.AdaptStreamOffline(e));
        }

        private async Task OnStreamOffline(EventSubStream.StreamOfflinePayload payload)
        {
            try
            {
                if (DidProcessMessage(payload.Metadata)) return;
                await StreamOffline();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }
        
        public async Task StreamOnline()
        {
            try
            {
                logger.LogInformation("Stream is online");
                eventService.IsOnline = true;
                await eventService.OnStreamStarted();
                await twitchEventActionHandler.HandleStreamOnlineAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnStreamOnline(object? sender, StreamOnlineArgs e)
        {
            await OnStreamOnline(EventSubAdapter.AdaptStreamOnline(e));
        }

        private async Task OnStreamOnline(EventSubStream.StreamOnlinePayload payload)
        {
            try
            {
                if (DidProcessMessage(payload.Metadata)) return;
                await StreamOnline();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            await OnChannelSubscription(EventSubAdapter.AdaptChannelSubscribe(e));
        }

        private async Task OnChannelSubscription(EventSubChannel.ChannelSubscribePayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("onChannelSubscription: {UserLogin} -- IsGift?: {IsGift} Tier- {Tier}"
                , payload.Event.UserLogin, payload.Event.IsGift, payload.Event.Tier);

                //if (await CheckIfPreviousSub(e.Payload.Event.UserLogin))
                //{
                //    logger.LogInformation("{UserLogin} previously subscribed, waiting for Renewal.", e.Payload.Event.UserLogin);
                //    return;
                //}

                //await subscriptionHistory.AddOrUpdateSubHistory(e.Payload.Event.UserLogin, e.Payload.Event.UserId);

                //if (CheckIfExistsAndAddSubCache(e.Payload.Event.UserLogin)) return;

                var subscriptionEventArgs = new Events.SubscriptionEventArgs
                {
                    Name = payload.Event.UserLogin,
                    UserId = payload.Event.UserId,
                    DisplayName = payload.Event.UserName,
                    IsGift = payload.Event.IsGift,
                    Tier = payload.Event.Tier,
                    HadPreviousSub = await CheckIfPreviousSub(payload.Event.UserLogin)
                };

                await eventService.OnSubscription(subscriptionEventArgs);
                await twitchEventActionHandler.HandleSubscribeAsync(subscriptionEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private Task<bool> CheckIfPreviousSub(string userLogin)
        {
            return subscriptionHistory.ExistingSub(userLogin);
        }

        private async Task OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            await OnChannelSubscriptionRenewal(EventSubAdapter.AdaptChannelSubscriptionRenewal(e));
        }

        private async Task OnChannelSubscriptionRenewal(EventSubChannel.ChannelSubscriptionRenewalPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("OnChannelSubscriptionRenewal: {UserLogin}", payload.Event.UserLogin);
                await subscriptionHistory.AddOrUpdateSubHistory(payload.Event.UserLogin, payload.Event.UserId);

                //if (CheckIfExistsAndAddSubCache(e.Payload.Event.UserLogin)) return;
                var subscriptionEventArgs = new Events.SubscriptionEventArgs
                {
                    Name = payload.Event.UserLogin,
                    UserId = payload.Event.UserId,
                    DisplayName = payload.Event.UserName,
                    Count = payload.Event.CumulativeMonths,
                    Streak = payload.Event.StreakMonths,
                    Tier = payload.Event.Tier,
                    IsRenewal = true,
                    Message = payload.Event.Message?.Text,
                    HadPreviousSub = await CheckIfPreviousSub(payload.Event.UserLogin)
                };

                await eventService.OnSubscription(subscriptionEventArgs);
                await twitchEventActionHandler.HandleSubscribeAsync(subscriptionEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            await OnChannelSubscriptionGift(EventSubAdapter.AdaptChannelSubscriptionGift(e));
        }

        private async Task OnChannelSubscriptionGift(EventSubChannel.ChannelSubscriptionGiftPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("OnChannelSubscriptionGift: {UserLogin}", payload.Event.UserLogin);

                var subscriptionGiftEventArgs = new Events.SubscriptionGiftEventArgs
                {
                    Name = payload.Event.UserLogin,
                    UserId = payload.Event.UserId,
                    DisplayName = payload.Event.UserName,
                    GiftAmount = payload.Event.Total,
                    TotalGifted = payload.Event.CumulativeTotal
                };

                await eventService.OnSubscriptionGift(subscriptionGiftEventArgs);
                await twitchEventActionHandler.HandleSubscriptionGiftAsync(subscriptionGiftEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            await OnChannelSubscriptionEnd(EventSubAdapter.AdaptChannelSubscriptionEnd(e));
        }

        private async Task OnChannelSubscriptionEnd(EventSubChannel.ChannelSubscriptionEndPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;

                logger.LogInformation("OnChannelSubscriptionEnd: {UserLogin}", payload.Event.UserLogin);

                var subscriptionEndEventArgs = new SubscriptionEndEventArgs
                {
                    Name = payload.Event.UserLogin,
                    UserId = payload.Event.UserId
                };

                await eventService.OnSubscriptionEnd(payload.Event.UserLogin, payload.Event.UserId);
                await twitchEventActionHandler.HandleSubscriptionEndAsync(subscriptionEndEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            await OnChannelCheer(EventSubAdapter.AdaptChannelCheer(e));
        }

        private async Task OnChannelCheer(EventSubChannel.ChannelCheerPayload payload)
        {
            try
            {
                if (DidProcessMessage(payload.Metadata)) return;
                logger.LogInformation("OnChannelCheer: {UserLogin}", payload.Event.UserLogin ?? "Anonymous");

                var cheerEventArgs = new Events.CheerEventArgs
                {
                    UserId = payload.Event.UserId,
                    Name = payload.Event.UserLogin,
                    DisplayName = payload.Event.UserName,
                    Amount = payload.Event.Bits,
                    Message = payload.Event.Message,
                    IsAnonymous = payload.Event.IsAnonymous
                };

                await eventService.OnCheer(payload.Event);
                await twitchEventActionHandler.HandleCheerAsync(cheerEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelBitsUse(object? sender, ChannelBitsUseArgs e)
        {
            await OnChannelBitsUse(EventSubAdapter.AdaptChannelBitsUse(e));
        }

        private async Task OnChannelBitsUse(EventSubChannel.ChannelBitsUsePayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("OnChannelBitsUse: {UserLogin}", payload.Event.UserLogin);

                var bitsUseEventArgs = new BitsUseEventArgs
                {
                    Name = payload.Event.UserLogin,
                    DisplayName = payload.Event.UserName,
                    Amount = payload.Event.Bits,
                    Message = payload.Event.Message?.Text,
                    UserId = payload.Event.UserId,
                    Type = payload.Event.Type,
                    BroadcasterUserId = payload.Event.BroadcasterUserId,
                    BroadcasterUserLogin = payload.Event.BroadcasterUserLogin,
                    BroadcasterUserName = payload.Event.BroadcasterUserName,
                    IsPowerUp = payload.Event.PowerUp != null,
                    PowerUp = payload.Event.PowerUp == null ? null : new PowerUp
                    {
                        Type = payload.Event.PowerUp.Type,
                        EmoteId = payload.Event.PowerUp.Emote?.Id,
                    },
                    IsCustomPowerUp = payload.Event.CustomPowerUp != null,
                    CustomPowerUp = payload.Event.CustomPowerUp == null ? null : new CustomPowerUp
                    {
                        Title = payload.Event.CustomPowerUp.Title,
                        RewardId = payload.Event.CustomPowerUp.RewardId,
                    },
                    HasBitsMessage = payload.Event.Message != null,
                    BitsMessage = payload.Event.Message == null ? null : new BitsMessage
                    {
                        Text = payload.Event.Message.Text,
                        Emotes = payload.Event.Message.Fragments?.Select(emote => new BitsEmote
                        {
                            Text = emote.Text,
                            Type = emote.Type,
                            EmoteId = emote.Emote?.Id,
                            EmoteSetId = emote.Emote?.EmoteSetId,
                            EmoteOwnerId = emote.Emote?.OwnerId,
                            EmoteFormat = emote.Emote?.Format,
                        }).ToList() ?? []
                    }
                };

                await twitchEventActionHandler.HandleBitsUseAsync(bitsUseEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }


        private async Task OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            await OnChannelPointRedeemed(EventSubAdapter.AdaptChannelPointRedemption(e));
        }

        private async Task OnChannelPointRedeemed(EventSubChannel.ChannelPointRedemptionPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                if (string.IsNullOrWhiteSpace(payload.Event.UserInput) == false) return; //Ignore wait for chat message

                var channelPointRedeemEventArgs = new ChannelPointRedeemEventArgs
                {
                    UserId = payload.Event.UserId,
                    Sender = payload.Event.UserName,
                    Username = payload.Event.UserLogin,
                    Title = payload.Event.Reward.Title,
                    UserInput = payload.Event.UserInput ?? string.Empty
                };

                await eventService.OnChannelPointRedeem(
                    payload.Event.UserId,
                    payload.Event.UserName,
                    payload.Event.Reward.Title);
                await twitchEventActionHandler.HandleChannelPointRedemptionAsync(channelPointRedeemEventArgs);
                logger.LogInformation("Channel pointed redeemed: {Title} by {user} status {status}", payload.Event.Reward.Title, payload.Event.UserName, payload.Event.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            await OnChannelFollow(EventSubAdapter.AdaptChannelFollow(e));
        }

        private async Task OnChannelFollow(EventSubChannel.ChannelFollowPayload payload)
        {
            try
            {
                if (DidProcessMessage(EventSubAdapter.MapMetadata(payload.Metadata))) return;
                logger.LogInformation("OnChannelFollow: {UserLogin}", payload.Event.UserLogin);

                var followEventArgs = new FollowEventArgs
                {
                    Username = payload.Event.UserLogin,
                    UserId = payload.Event.UserId,
                    DisplayName = payload.Event.UserName,
                    FollowDate = payload.Event.FollowedAt
                };

                await eventService.OnFollow(payload.Event);
                await twitchEventActionHandler.HandleFollowAsync(followEventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }


        private Task MessageReceived(object? sender, MessageReceivedEventArgs args)
        {
            LastMessageReceived = timeProvider.GetLocalNow();
            return Task.CompletedTask;
        }

        private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            logger.LogInformation(e.Exception, "Websocket error occured: {message}", e.Message);

            await Reconnect();
        }

        private Task OnWebsocketReconnected(object? sender, EventArgs e)
        {
            logger.LogWarning("Twitch Websocket {SessionId} reconnected", eventSubWebsocketClient.SessionId);
            return Task.CompletedTask;
        }

        private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
        {
            await Reconnect();
        }

        public async Task Reconnect()
        {
            if (Reconnecting)
            {
                logger.LogWarning("Already reconnecting, ignoring");
                return;
            }
            
            Reconnecting = true;
            try
            {
                logger.LogWarning("Twitch Websocket Disconnected");
                var delayCounter = 1;
                while (true)
                {
                    try
                    {
                        if(!await twitchService.ValidateAndRefreshToken()) throw new Exception("Failed to refresh token");

                        logger.LogWarning("Attempting to reconnect to Twitch Websocket");
                        if (await eventSubWebsocketClient.ReconnectAsync())
                        {
                            logger.LogInformation("Twitch Websocket Reconnected");
                            return;
                        }
                        logger.LogWarning("Twitch Websocket Reconnect failed");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reconnecting to Twitch Websocket");
                    }
                    delayCounter *= 2;
                    if (delayCounter > 30) delayCounter = 30;
                    logger.LogError("Twitch Websocket reconnection failed! Attempting again in {delayCounter} seconds.", delayCounter);
                    await Task.Delay(delayCounter * 1000);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception when trying to reconnect after being disconnected");
            }
            finally
            {
                Reconnecting = false;
            }
        }

        private async Task Connect()
        {
            try
            {
                var delayCounter = 1;
                while (true)
                {
                    try
                    {
                        
                        //if (await eventSubWebsocketClient.ConnectAsync(new Uri("ws://127.0.0.1:8080/ws"))) return;
                        if (await eventSubWebsocketClient.ConnectAsync()) return;
                    }
                    catch (Exception)
                    {
                        //Ignore
                    }
                    delayCounter *= 2;
                    if (delayCounter > 30)
                    {
                        delayCounter = 30;
                    }
                    await Task.Delay(delayCounter * 1000);
                    logger.LogError("Twitch Websocket connected failed! Attempting again in {delayCounter} seconds.", delayCounter);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception when trying to connect after being reconnect failed.");
            }
        }


        private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            logger.LogInformation("Twitch Websocket connected");
            if (e.IsRequestedReconnect) return;
            try
            {
                if (await twitchService.SubscribeToAllTheStuffs(eventSubWebsocketClient.SessionId))
                {
                    logger.LogInformation("Subscribed to events");
                    KeepAliveTimer = e.KeepAliveTimeout;
                    JoinTimer?.Dispose();
                    JoinTimer = timeProvider.CreateTimer(CheckWebsocketStatus, this, KeepAliveTimer, KeepAliveTimer);
                }
                else
                {
                    logger.LogError("Failed to subscribe to events");
                    await Reconnect();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error subscribing to the events");
                await Reconnect();
            }
        }

        // async void cause timer does not support Tasks
        private async void CheckWebsocketStatus(object? state)
        {
            try
            {
                if (KeepAliveTimer == TimeSpan.MinValue) return;
                //Double the time, the library should handle it before then
                if (LastMessageReceived + (KeepAliveTimer * 2) < timeProvider.GetLocalNow() &&
                    twitchService.IsServiceUp())
                {
                    logger.LogWarning("Websocket not receiving messages for {KeepAliveTimer} seconds, reconnecting", KeepAliveTimer.TotalSeconds * 2);
                    await Reconnect();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking websocket status");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Twitch Websocket");
            eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
            eventSubWebsocketClient.ChannelCheer += OnChannelCheer;
            eventSubWebsocketClient.ChannelSubscribe += OnChannelSubscription;
            eventSubWebsocketClient.ChannelSubscriptionGift += OnChannelSubscriptionGift;
            eventSubWebsocketClient.ChannelSubscriptionEnd += OnChannelSubscriptionEnd;
            eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionRenewal;
            eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointRedeemed;
            eventSubWebsocketClient.ChannelRaid += OnChannelRaid;

            eventSubWebsocketClient.ChannelBitsUse += OnChannelBitsUse;

            eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            eventSubWebsocketClient.ChannelBan += OnChannelBan;
            eventSubWebsocketClient.ChannelUnban += OnChannelUnBan;

            eventSubWebsocketClient.ChannelAdBreakBegin += ChannelAdBreakBegin;
            eventSubWebsocketClient.ChannelChatMessage += ChannelChatMessage;
            eventSubWebsocketClient.ChannelChatNotification += OnChannelChatNotification;
            eventSubWebsocketClient.ChannelSuspiciousUserMessage += ChannelSuspiciousUserMessage;
            eventSubWebsocketClient.ChannelChatMessageDelete += ChannelChatMessageDelete;
            eventSubWebsocketClient.MessageReceived += MessageReceived;
            eventService.IsOnline = await twitchService.IsStreamOnline();
            
            await Connect();
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping Twitch Websocket");
            await eventSubWebsocketClient.DisconnectAsync();
            eventSubWebsocketClient.WebsocketConnected -= OnWebsocketConnected;
            eventSubWebsocketClient.WebsocketDisconnected -= OnWebsocketDisconnected;
            eventSubWebsocketClient.WebsocketReconnected -= OnWebsocketReconnected;
            eventSubWebsocketClient.ErrorOccurred -= OnErrorOccurred;

            eventSubWebsocketClient.ChannelFollow -= OnChannelFollow;
            eventSubWebsocketClient.ChannelCheer -= OnChannelCheer;
            eventSubWebsocketClient.ChannelSubscribe -= OnChannelSubscription;
            eventSubWebsocketClient.ChannelSubscriptionGift -= OnChannelSubscriptionGift;
            eventSubWebsocketClient.ChannelSubscriptionEnd -= OnChannelSubscriptionEnd;
            eventSubWebsocketClient.ChannelSubscriptionMessage -= OnChannelSubscriptionRenewal;
            eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointRedeemed;
            eventSubWebsocketClient.ChannelRaid -= OnChannelRaid;

            eventSubWebsocketClient.StreamOnline -= OnStreamOnline;
            eventSubWebsocketClient.StreamOffline -= OnStreamOffline;
            eventSubWebsocketClient.ChannelBan -= OnChannelBan;
            eventSubWebsocketClient.ChannelUnban -= OnChannelUnBan;

            eventSubWebsocketClient.ChannelBitsUse -= OnChannelBitsUse;
            eventSubWebsocketClient.ChannelAdBreakBegin -= ChannelAdBreakBegin;
            eventSubWebsocketClient.ChannelChatMessage -= ChannelChatMessage;
            eventSubWebsocketClient.ChannelChatNotification -= OnChannelChatNotification;
            eventSubWebsocketClient.ChannelSuspiciousUserMessage -= ChannelSuspiciousUserMessage;
            eventSubWebsocketClient.ChannelChatMessageDelete -= ChannelChatMessageDelete;
        }

        [GeneratedRegex(@"[^\u0000-\u00FF]+")]
        private static partial Regex MessageRegex();
    }
}

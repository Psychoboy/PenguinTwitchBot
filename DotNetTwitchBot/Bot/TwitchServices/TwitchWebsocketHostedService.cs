using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public partial class TwitchWebsocketHostedService(
        ILogger<TwitchWebsocketHostedService> logger,
        IServiceBackbone eventService,
        EventSubWebsocketClient eventSubWebsocketClient,
        SubscriptionTracker subscriptionHistory,
        ChatMessageIdTracker messageIdTracker,
        IMemoryCache memoryCache,
        ITwitchService twitchService,
        IMediator mediator) : ITwitchWebsocketHostedService
    {
        private readonly ConcurrentDictionary<string, DateTime> SubCache = new();
        static readonly SemaphoreSlim _subscriptionLock = new(1);
        private bool Reconnecting = false;

        private async Task ChannelChatMessage(object sender, ChannelChatMessageArgs args)
        {
            if (messageIdTracker.IsSelfMessage(args.Notification.Payload.Event.MessageId)) return;
            if (DidProcessMessage(args.Notification.Metadata)) return;

            var messageText = args.Notification.Payload.Event.Message.Text;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();

            if (string.IsNullOrEmpty(args.Notification.Payload.Event.ChannelPointsCustomRewardId) == false)
            {
                var channelPoint = await twitchService.GetCustomReward(args.Notification.Payload.Event.ChannelPointsCustomRewardId);
                if (channelPoint == null)
                {
                    logger.LogError("Failed to get channel point");
                    return;
                }
                await eventService.OnChannelPointRedeem(
                    args.Notification.Payload.Event.ChatterUserId,
                   args.Notification.Payload.Event.ChatterUserName.ToLower(),
                   channelPoint.Title,
                   args.Notification.Payload.Event.Message.Text);
                logger.LogInformation("Channel pointed redeemed: {Title} by {user} userInput: {userInput}", channelPoint.Title, args.Notification.Payload.Event.ChatterUserName, messageText);
            }
            else
            {
                logger.LogInformation("CHATMSG: {name}: {message}", args.Notification.Payload.Event.ChatterUserName, messageText);
                var e = args.Notification.Payload.Event;
                await Task.WhenAll([ProcessCommandMessage(e), ProcessChatMessage(e)]);
            }
        }

        private Task ChannelChatMessageDelete(object sender, ChannelChatMessageDeleteArgs args)
        {
            return mediator.Publish(new DeletedChatMessage { EventArgs = args });
        }

        private Task ChannelSuspiciousUserMessage(object sender, ChannelSuspiciousUserMessageArgs args)
        {
            if (messageIdTracker.IsSelfMessage(args.Notification.Payload.Event.Message.MessageId)) return Task.CompletedTask;
            if (DidProcessMessage(args.Notification.Metadata)) return Task.CompletedTask;
            logger.LogInformation("SUSPICIOUS CHAT: {name}: {message}", args.Notification.Payload.Event.UserName, args.Notification.Payload.Event.Message);
            var e = args.Notification.Payload.Event;
            var messageText = args.Notification.Payload.Event.Message.Text;
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
            return mediator.Publish(new ReceivedChatMessage { EventArgs = chatMessage });
        }

        private Task ProcessChatMessage(ChannelChatMessage e)
        {
            var messageText = e.Message.Text;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();
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
                FromOwnChannel = string.IsNullOrWhiteSpace(e.SourceBroadcasterUserId)

            };
            return mediator.Publish(new ReceivedChatMessage { EventArgs = chatMessage });
        }

        private async Task ProcessCommandMessage(ChannelChatMessage e)
        {
            if (e.Message.Text.StartsWith('!') == false) return;
            var messageText = e.Message.Text;
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

        private async Task ChannelAdBreakBegin(object sender, ChannelAdBreakBeginArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("Ad Begin. Length: {length} Started At: {startedAt} Automatic: {automatic}", e.Notification.Payload.Event.DurationSeconds, e.Notification.Payload.Event.StartedAt, e.Notification.Payload.Event.IsAutomatic);
                var ev = new AdBreakStartEventArgs
                {
                    Automatic = e.Notification.Payload.Event.IsAutomatic,
                    Length = e.Notification.Payload.Event.DurationSeconds,
                    StartedAt = e.Notification.Payload.Event.StartedAt
                };
                await AdBreak(ev);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelUnBan(object? sender, ChannelUnbanArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("OnChannelUnBan {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await eventService.OnViewerBan(e.Notification.Payload.Event.UserId, e.Notification.Payload.Event.UserLogin, true, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelBan(object? sender, ChannelBanArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                if (e.Notification.Payload.Event.IsPermanent == false)
                {
                    logger.LogInformation("{UserLogin} timed out by {Moderator}.", e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.ModeratorUserLogin);
                    return;
                }
                logger.LogInformation("{UserLogin} banned by {Moderator}", e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.ModeratorUserLogin);
                
                await eventService.OnViewerBan(e.Notification.Payload.Event.UserId, e.Notification.Payload.Event.UserLogin, false, e.Notification.Payload.Event.EndsAt);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelRaid(object? sender, ChannelRaidArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;

                logger.LogInformation("OnChannelRaid from {BroadcasterName}", e.Notification.Payload.Event.FromBroadcasterUserName);
                await eventService.OnIncomingRaid(new Events.RaidEventArgs
                {
                    Name = e.Notification.Payload.Event.FromBroadcasterUserLogin,
                    UserId = e.Notification.Payload.Event.FromBroadcasterUserId,
                    DisplayName = e.Notification.Payload.Event.FromBroadcasterUserName,
                    NumberOfViewers = e.Notification.Payload.Event.Viewers
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private bool DidProcessMessage(EventSubMetadata metadata)
        {
            if (memoryCache.TryGetValue(metadata.MessageId, out var messageId))
            {
                logger.LogWarning("Already processed message: {MessageId} - {MessageType} - {MessageTimestamp}", metadata.MessageId, metadata.MessageType, metadata.MessageTimestamp);
                return true;
            }

            memoryCache.Set(metadata.MessageId, metadata.MessageId, TimeSpan.FromMinutes(10));
            return false;

        }

        public async Task StreamOffline()
        {
            try
            {
                logger.LogInformation("Stream is offline");
                eventService.IsOnline = false;
                await eventService.OnStreamEnded();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnStreamOffline(object? sender, StreamOfflineArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnStreamOnline(object? sender, StreamOnlineArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                await StreamOnline();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("onChannelSubscription: {UserLogin} -- IsGift?: {IsGift} Type: {SubscriptionType} Tier- {Tier}"
                , e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.IsGift, e.Notification.Metadata.SubscriptionType, e.Notification.Payload.Event.Tier);

                await subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.UserId);

                if (await CheckIfPreviousSub(e.Notification.Payload.Event.UserLogin))
                {
                    logger.LogInformation("{UserLogin} previously subscribed, waiting for Renewal.", e.Notification.Payload.Event.UserLogin);
                    return;
                }

                if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;

                await eventService.OnSubscription(new Events.SubscriptionEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    UserId = e.Notification.Payload.Event.UserId,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    IsGift = e.Notification.Payload.Event.IsGift
                });
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
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("OnChannelSubscriptionRenewal: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.UserId);

                if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;
                await eventService.OnSubscription(new Events.SubscriptionEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    UserId = e.Notification.Payload.Event.UserId,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    Count = e.Notification.Payload.Event.CumulativeMonths,
                    Streak = e.Notification.Payload.Event.StreakMonths,
                    IsRenewal = true,
                    Message = e.Notification.Payload.Event.Message?.Text
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("OnChannelSubscriptionGift: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await eventService.OnSubscriptionGift(new Events.SubscriptionGiftEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    UserId = e.Notification.Payload.Event.UserId,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    GiftAmount = e.Notification.Payload.Event.Total,
                    TotalGifted = e.Notification.Payload.Event.CumulativeTotal
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;

                logger.LogInformation("OnChannelSubscriptionEnd: {UserLogin} Type: {SubscriptionType}", e.Notification.Payload.Event.UserLogin, e.Notification.Metadata.SubscriptionType);
                await eventService.OnSubscriptionEnd(e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.UserId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private bool CheckIfExistsAndAddSubCache(string name)
        {
            try
            {
                _subscriptionLock.Wait();

                if (string.IsNullOrWhiteSpace(name))
                {
                    logger.LogWarning("Subscriber name was null or white space");
                    return false;
                }
                if (SubCache.TryGetValue(name, out var subTime) && subTime > DateTime.Now.AddDays(-5))
                {
                    logger.LogWarning("{name} Subscriber already in sub cache", name);
                    return true;
                }
                SubCache[name] = DateTime.Now;
                return false;
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        private async Task OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("OnChannelCheer: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await eventService.OnCheer(e.Notification.Payload.Event);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                if (string.IsNullOrWhiteSpace(e.Notification.Payload.Event.UserInput) == false) return; //Ignore wait for chat message
                await eventService.OnChannelPointRedeem(
                    e.Notification.Payload.Event.UserId,
                    e.Notification.Payload.Event.UserName,
                    e.Notification.Payload.Event.Reward.Title);
                logger.LogInformation("Channel pointed redeemed: {Title} by {user} status {status}", e.Notification.Payload.Event.Reward.Title, e.Notification.Payload.Event.UserName, e.Notification.Payload.Event.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                logger.LogInformation("OnChannelFollow: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await eventService.OnFollow(e.Notification.Payload.Event);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            logger.LogDebug(e.Exception, "Websocket error occured: {message}", e.Message);

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
            if (Reconnecting) return;
            Reconnecting = true;
            try
            {
                logger.LogWarning("Twitch Websocket Disconnected");
                var delayCounter = 1;
                while (true)
                {
                    try
                    {
                        logger.LogWarning("Attempting to reconnect to Twitch Websocket");
                        if (await eventSubWebsocketClient.ReconnectAsync()) return;
                    }
                    catch (Exception)
                    {
                        //Ignore
                    }
                    delayCounter *= 2;
                    if (delayCounter > 60) delayCounter = 60;
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
                        
                        if (await eventSubWebsocketClient.ConnectAsync()) return;
                    }
                    catch (Exception)
                    {
                        //Ignore
                    }
                    delayCounter *= 2;
                    if (delayCounter > 300)
                    {
                        delayCounter = 300;
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
                await twitchService.SubscribeToAllTheStuffs(eventSubWebsocketClient.SessionId);
                logger.LogInformation("Subscribed to events");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error subscribing to the events");
                await Reconnect();
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

            eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            eventSubWebsocketClient.ChannelBan += OnChannelBan;
            eventSubWebsocketClient.ChannelUnban += OnChannelUnBan;

            eventSubWebsocketClient.ChannelAdBreakBegin += ChannelAdBreakBegin;
            eventSubWebsocketClient.ChannelChatMessage += ChannelChatMessage;
            eventSubWebsocketClient.ChannelSuspiciousUserMessage += ChannelSuspiciousUserMessage;
            eventSubWebsocketClient.ChannelChatMessageDelete += ChannelChatMessageDelete;
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

            eventSubWebsocketClient.ChannelAdBreakBegin -= ChannelAdBreakBegin;
            eventSubWebsocketClient.ChannelChatMessage -= ChannelChatMessage;
            eventSubWebsocketClient.ChannelSuspiciousUserMessage -= ChannelSuspiciousUserMessage;
            eventSubWebsocketClient.ChannelChatMessageDelete -= ChannelChatMessageDelete;
            //eventSubWebsocketClient.Mod

        }

        [GeneratedRegex(@"[^\u0000-\u00FF]+")]
        private static partial Regex MessageRegex();
    }
}
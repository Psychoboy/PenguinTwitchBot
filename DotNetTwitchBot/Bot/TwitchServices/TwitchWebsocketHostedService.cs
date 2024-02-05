using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchWebsocketHostedService : IHostedService
    {
        private readonly ILogger<TwitchWebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        private readonly IMemoryCache _eventIdCache;
        private readonly ITwitchService _twitchService;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly SubscriptionTracker _subscriptionHistory;
        private readonly ChatMessageIdTracker _messageIdTracker;
        private readonly ConcurrentDictionary<string, DateTime> SubCache = new();
        static readonly SemaphoreSlim _subscriptionLock = new(1);
        private static bool Reconnecting { get; set; } = false;

        public TwitchWebsocketHostedService(
            ILogger<TwitchWebsocketHostedService> logger,
            IServiceBackbone eventService,
            EventSubWebsocketClient eventSubWebsocketClient,
            SubscriptionTracker subscriptionHistory,
            ChatMessageIdTracker messageIdTracker,
            IMemoryCache memoryCache,
            ITwitchService twitchService)
        {
            _logger = logger;
            _eventSubWebsocketClient = eventSubWebsocketClient;
            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            _eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
            _eventSubWebsocketClient.ChannelCheer += OnChannelCheer;
            _eventSubWebsocketClient.ChannelSubscribe += OnChannelSubscription;
            _eventSubWebsocketClient.ChannelSubscriptionGift += OnChannelSubscriptionGift;
            _eventSubWebsocketClient.ChannelSubscriptionEnd += OnChannelSubscriptionEnd;
            _eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionRenewal;
            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointRedeemed;
            _eventSubWebsocketClient.ChannelRaid += OnChannelRaid;

            _eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            _eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            _eventSubWebsocketClient.ChannelBan += OnChannelBan;
            _eventSubWebsocketClient.ChannelUnban += OnChannelUnBan;

            _eventSubWebsocketClient.ChannelAdBreakBegin += ChannelAdBreakBegin;
            _eventSubWebsocketClient.ChannelChatMessage += ChannelChatMessage;


            _twitchService = twitchService;
            _serviceBackbone = eventService;
            _subscriptionHistory = subscriptionHistory;
            _messageIdTracker = messageIdTracker;
            _eventIdCache = memoryCache;


        }

        private Task ChannelChatMessage(object sender, ChannelChatMessageArgs args)
        {
            if (_messageIdTracker.IsSelfMessage(args.Notification.Payload.Event.MessageId)) return Task.CompletedTask;
            if (DidProcessMessage(args.Notification.Metadata)) return Task.CompletedTask;
            _logger.LogInformation("CHATMSG: {name}: {message}", args.Notification.Payload.Event.ChatterUserName, args.Notification.Payload.Event.Message.Text);
            var e = args.Notification.Payload.Event;
            return Task.WhenAll([ProcessCommandMessage(e), ProcessChatMessage(e)]);
        }

        private Task ProcessChatMessage(ChannelChatMessage e)
        {
            var chatMessage = new ChatMessageEventArgs
            {
                Message = e.Message.Text,
                Name = e.ChatterUserLogin.ToLower(),
                DisplayName = e.ChatterUserName,
                IsSub = e.IsSubscriber,
                IsMod = e.IsModerator,
                IsVip = e.IsVip,
                IsBroadcaster = e.IsBroadcaster
            };
            return _serviceBackbone.OnChatMessage(chatMessage);
        }

        private Task ProcessCommandMessage(ChannelChatMessage e)
        {
            if (e.Message.Text.StartsWith('!') == false) return Task.CompletedTask;

            var argsFull = e.Message.Text.Split(' ', 2);
            var command = argsFull[0];
            var ArgumentsAsString = argsFull.Length > 1 ? argsFull[1] : "";
            var ArgumentsAsList = string.IsNullOrWhiteSpace(ArgumentsAsString) ? [] : ArgumentsAsString.Split(" ").ToList();
            var eventArgs = new CommandEventArgs
            {
                Command = command[1..].ToLower(),
                Arg = ArgumentsAsString,
                Args = ArgumentsAsList,
                IsWhisper = false,
                Name = e.ChatterUserLogin,
                DisplayName = e.ChatterUserName,
                IsSub = e.IsSubscriber,
                IsMod = e.IsModerator,
                IsVip = e.IsVip,
                IsBroadcaster = e.IsBroadcaster,
                TargetUser = ArgumentsAsList.Count > 0 ? ArgumentsAsList[0].Replace("@", "").Trim().ToLower() : ""
            };
            return _serviceBackbone.OnCommand(eventArgs);
        }

        private async Task ChannelAdBreakBegin(object sender, ChannelAdBreakBeginArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("Ad Begin. Length: {length} Started At: {startedAt} Automatic: {automatic}", e.Notification.Payload.Event.DurationSeconds, e.Notification.Payload.Event.StartedAt, e.Notification.Payload.Event.IsAutomatic);
                var ev = new AdBreakStartEventArgs
                {
                    Automatic = e.Notification.Payload.Event.IsAutomatic,
                    Length = e.Notification.Payload.Event.DurationSeconds,
                    StartedAt = e.Notification.Payload.Event.StartedAt
                };
                await _serviceBackbone.OnAdBreakStartEvent(ev);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelUnBan(object? sender, ChannelUnbanArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("OnChannelUnBan {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await _serviceBackbone.OnViewerBan(e.Notification.Payload.Event.UserLogin, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelBan(object? sender, ChannelBanArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                if (e.Notification.Payload.Event.IsPermanent == false)
                {
                    _logger.LogInformation("{UserLogin} timed out by {Moderator}.", e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.ModeratorUserLogin);
                    return;
                }
                _logger.LogInformation("{UserLogin} banned by {Moderator}", e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.ModeratorUserLogin);
                await _serviceBackbone.OnViewerBan(e.Notification.Payload.Event.UserLogin, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelRaid(object? sender, ChannelRaidArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;

                _logger.LogInformation("OnChannelRaid from {BroadcasterName}", e.Notification.Payload.Event.FromBroadcasterUserName);
                await _serviceBackbone.OnIncomingRaid(new Events.RaidEventArgs
                {
                    Name = e.Notification.Payload.Event.FromBroadcasterUserLogin,
                    DisplayName = e.Notification.Payload.Event.FromBroadcasterUserName,
                    NumberOfViewers = e.Notification.Payload.Event.Viewers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private bool DidProcessMessage(EventSubMetadata metadata)
        {
            if (_eventIdCache.TryGetValue(metadata.MessageId, out var messageId))
            {
                _logger.LogWarning("Already processed message: {MessageId} - {MessageType} - {MessageTimestamp}", metadata.MessageId, metadata.MessageType, metadata.MessageTimestamp);
                return true;
            }

            _eventIdCache.Set(metadata.MessageId, metadata.MessageId, TimeSpan.FromMinutes(10));
            return false;

        }

        private async Task OnStreamOffline(object? sender, StreamOfflineArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("Stream is offline");
                _serviceBackbone.IsOnline = false;
                await _serviceBackbone.OnStreamEnded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnStreamOnline(object? sender, StreamOnlineArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("Stream is online");
                _serviceBackbone.IsOnline = true;
                await _serviceBackbone.OnStreamStarted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("onChannelSubscription: {UserLogin} -- IsGift?: {IsGift} Type: {SubscriptionType} Tier- {Tier}"
                , e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.IsGift, e.Notification.Metadata.SubscriptionType, e.Notification.Payload.Event.Tier);

                await _subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin);

                if (await CheckIfPreviousSub(e.Notification.Payload.Event.UserLogin))
                {
                    _logger.LogInformation("{UserLogin} previously subscribed, waiting for Renewal.", e.Notification.Payload.Event.UserLogin);
                    return;
                }

                if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;

                await _serviceBackbone.OnSubscription(new Events.SubscriptionEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    IsGift = e.Notification.Payload.Event.IsGift
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private Task<bool> CheckIfPreviousSub(string userLogin)
        {
            return _subscriptionHistory.ExistingSub(userLogin);
        }

        private async Task OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("OnChannelSubscriptionRenewal: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await _subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin);

                if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;
                await _serviceBackbone.OnSubscription(new Events.SubscriptionEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    Count = e.Notification.Payload.Event.CumulativeMonths,
                    Streak = e.Notification.Payload.Event.StreakMonths,
                    IsRenewal = true,
                    Message = e.Notification.Payload.Event.Message?.Text
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("OnChannelSubscriptionGift: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await _serviceBackbone.OnSubscriptionGift(new Events.SubscriptionGiftEventArgs
                {
                    Name = e.Notification.Payload.Event.UserLogin,
                    DisplayName = e.Notification.Payload.Event.UserName,
                    GiftAmount = e.Notification.Payload.Event.Total,
                    TotalGifted = e.Notification.Payload.Event.CumulativeTotal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;

                _logger.LogInformation("OnChannelSubscriptionEnd: {UserLogin} Type: {SubscriptionType}", e.Notification.Payload.Event.UserLogin, e.Notification.Metadata.SubscriptionType);
                await _serviceBackbone.OnSubscriptionEnd(e.Notification.Payload.Event.UserLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private bool CheckIfExistsAndAddSubCache(string name)
        {
            try
            {
                _subscriptionLock.Wait();

                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning("Subscriber name was null or white space");
                    return false;
                }
                if (SubCache.TryGetValue(name, out var subTime) && subTime > DateTime.Now.AddDays(-5))
                {
                    _logger.LogWarning("{name} Subscriber already in sub cache", name);
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
                _logger.LogInformation("OnChannelCheer: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await _serviceBackbone.OnCheer(e.Notification.Payload.Event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                await _serviceBackbone.OnChannelPointRedeem(
                    e.Notification.Payload.Event.UserName,
                    e.Notification.Payload.Event.Reward.Title,
                    e.Notification.Payload.Event.UserInput);
                _logger.LogInformation("Channel pointed redeemed: {Title}", e.Notification.Payload.Event.Reward.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            try
            {
                if (DidProcessMessage(e.Notification.Metadata)) return;
                _logger.LogInformation("OnChannelFollow: {UserLogin}", e.Notification.Payload.Event.UserLogin);
                await _serviceBackbone.OnFollow(e.Notification.Payload.Event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in websocket message");
            }
        }

        private Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            _logger.LogError(e.Exception, "Websocket error occured: {message}", e.Message);

            return ForceReconnect();
        }

        private Task OnWebsocketReconnected(object? sender, EventArgs e)
        {
            _logger.LogWarning("Twitch Websocket {SessionId} reconnected", _eventSubWebsocketClient.SessionId);
            return Task.CompletedTask;
        }

        private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
        {
            await ForceReconnect();
        }

        public async Task ForceReconnect()
        {
            if (Reconnecting) return;
            try
            {
                Reconnecting = true;
                _logger.LogWarning("Twitch Websocket Disconnected");
                var delayCounter = 1;
                var attempts = 0;
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    delayCounter *= 2;
                    attempts++;
                    if (attempts > 5) break;
                    if (delayCounter > 60) delayCounter = 60;
                    _logger.LogError("Twitch Websocket reconnection failed! Attempting again in {delayCounter} seconds.", delayCounter);
                    await Task.Delay(delayCounter * 1000);
                }
                if (attempts > 5)
                {
                    await Reconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to reconnect after being disconnected");
            }
            finally { Reconnecting = false; }
        }

        private async Task Reconnect()
        {
            if (Reconnecting) return;
            try
            {
                Reconnecting = true;
                var delayCounter = 1;
                while (!await _eventSubWebsocketClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws")))
                {
                    delayCounter *= 2;
                    if (delayCounter > 300)
                    {
                        delayCounter = 300;
                    }
                    await Task.Delay(delayCounter * 1000);
                    _logger.LogError("Twitch Websocket connected failed! Attempting again in {delayCounter} seconds.", delayCounter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to connect after being reconnect failed.");
            }
            finally { Reconnecting = false; }
        }


        private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            _logger.LogInformation("Twitch Websocket connected");
            if (e.IsRequestedReconnect) return;
            try
            {
                await _twitchService.SubscribeToAllTheStuffs(_eventSubWebsocketClient.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to the events");
                if (!await _eventSubWebsocketClient.DisconnectAsync())
                {
                    _logger.LogWarning("Failed to disconnect when requested");
                }
                await Reconnect();
            }
            _logger.LogInformation("Subscribed to events");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }
    }
}
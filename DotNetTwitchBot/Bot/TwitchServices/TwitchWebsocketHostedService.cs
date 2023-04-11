using System.Collections.Concurrent;
using System.Diagnostics;
using DotNetTwitchBot.Bot.Core;
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
        private ConcurrentBag<string> MessageIds = new ConcurrentBag<string>();
        private TwitchService _twitchService;
        private ServiceBackbone _eventService;
        private ConcurrentDictionary<string, DateTime> SubCache = new ConcurrentDictionary<string, DateTime>();

        public TwitchWebsocketHostedService(
            ILogger<TwitchWebsocketHostedService> logger,
            ServiceBackbone eventService,
            EventSubWebsocketClient eventSubWebsocketClient,
            TwitchService twitchService)
        {
            _logger = logger;
            _eventSubWebsocketClient = eventSubWebsocketClient;
            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            _eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
            _eventSubWebsocketClient.ChannelCheer += OnChannelCheer;
            _eventSubWebsocketClient.ChannelSubscribe += onChannelSubscription;
            _eventSubWebsocketClient.ChannelSubscriptionGift += OnChannelSubscriptionGift;
            _eventSubWebsocketClient.ChannelSubscriptionEnd += OnChannelSubscriptionEnd;
            _eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionRenewal;
            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointRedeemed;
            _eventSubWebsocketClient.ChannelRaid += OnChannelRaid;

            _eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            _eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            _twitchService = twitchService;
            _eventService = eventService;
        }


        private async void OnChannelRaid(object? sender, ChannelRaidArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;

            _logger.LogInformation("OnChannelRaid from {0}", e.Notification.Payload.Event.FromBroadcasterUserName);
            await _eventService.OnIncomingRaid(new Events.RaidEventArgs
            {
                Name = e.Notification.Payload.Event.FromBroadcasterUserLogin,
                DisplayName = e.Notification.Payload.Event.FromBroadcasterUserName,
                NumberOfViewers = e.Notification.Payload.Event.Viewers
            });
        }

        private bool DidProcessMessage(EventSubMetadata metadata)
        {
            if (MessageIds.Contains(metadata.MessageId))
            {
                return true;
            }
            else
            {
                MessageIds.Add(metadata.MessageId);
                return false;
            }
        }

        private async void OnStreamOffline(object? sender, StreamOfflineArgs e)
        {
            _logger.LogInformation("Stream is offline");
            _eventService.IsOnline = false;
            await _eventService.OnStreamEnded();
        }

        private async void OnStreamOnline(object? sender, StreamOnlineArgs e)
        {
            _logger.LogInformation("Stream is online");
            _eventService.IsOnline = true;
            await _eventService.OnStreamStarted();
        }

        private async void OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelSubscriptionRenewal: {0}", e.Notification.Payload.Event.UserName);
            if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserName)) return;
            SubCache[e.Notification.Payload.Event.UserName] = DateTime.Now;
            await _eventService.OnSubscription(new Events.SubscriptionEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                Count = e.Notification.Payload.Event.CumulativeTotal
            });
        }

        private async void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelSubscriptionGift: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnSubscriptionGift(new Events.SubscriptionGiftEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                GiftAmount = e.Notification.Payload.Event.Total,
                TotalGifted = e.Notification.Payload.Event.CumulativeTotal
            });
        }

        private async void onChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;

            _logger.LogInformation("onChannelSubscription: {0} -- IsGift?: {1} Type: {2} Tier- {3}"
            , e.Notification.Payload.Event.UserName, e.Notification.Payload.Event.IsGift, e.Notification.Metadata.SubscriptionType, e.Notification.Payload.Event.Tier);
            if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserName)) return;

            SubCache[e.Notification.Payload.Event.UserName] = DateTime.Now;

            await _eventService.OnSubscription(new Events.SubscriptionEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                IsGift = e.Notification.Payload.Event.IsGift
            });
        }

        private async void OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;

            _logger.LogInformation("OnChannelSubscriptionEnd: {0} Type: {1}", e.Notification.Payload.Event.UserName, e.Notification.Metadata.SubscriptionType);
            await _eventService.OnSubscriptionEnd(e.Notification.Payload.Event.UserLogin);
        }

        private bool CheckIfExistsAndAddSubCache(string name)
        {
            if (SubCache.ContainsKey(name) && SubCache[name] > DateTime.Now.AddMinutes(-5)) return true;
            SubCache[name] = DateTime.Now;
            return false;
        }

        private async void OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelCheer: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnCheer(e.Notification.Payload.Event);
        }

        private async void OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            await _eventService.OnChannelPointRedeem(
                e.Notification.Payload.Event.UserName,
                e.Notification.Payload.Event.Reward.Title,
                e.Notification.Payload.Event.UserInput);
            _logger.LogInformation("Channel pointed redeemed: {0}", e.Notification.Payload.Event.Reward.Title);
        }

        private async void OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelFollow: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnFollow(e.Notification.Payload.Event);
        }

        private void OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            _logger.LogError(e.Message);
        }

        private void OnWebsocketReconnected(object? sender, EventArgs e)
        {
            _logger.LogWarning("Websocket {SessionId} reconnected", _eventSubWebsocketClient.SessionId);
        }

        private async void OnWebsocketDisconnected(object? sender, EventArgs e)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                bool fullConnect = false;
                _logger.LogWarning("Websocket Disconnected");
                var delayCounter = 1.0;
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    delayCounter = (delayCounter * 2);
                    if (delayCounter > 60.0) delayCounter = 60.0;
                    _logger.LogError("Websocket reconnected failed! Attempting again in {0} seconds.", delayCounter);
                    await Task.Delay((int)delayCounter * 1000);
                    if (stopwatch.Elapsed.TotalSeconds >= 30.0)
                    {
                        fullConnect = true;
                        break;
                    }
                }
                if (fullConnect)
                {
                    await Reconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to reconnect after being disconnected");
            }
        }

        private async Task Reconnect()
        {
            try
            {
                var delayCounter = 1;
                while (!await _eventSubWebsocketClient.ConnectAsync())
                {
                    delayCounter = (delayCounter * 2);
                    if (delayCounter > 300) delayCounter = 300;
                    Thread.Sleep(delayCounter);
                    _logger.LogError("Websocket connected failed! Attempting again in {0} seconds.", delayCounter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to connect after being reconnect failed.");
            }
        }

        private async void OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            _logger.LogInformation("Websocket connected");
            if (e.IsRequestedReconnect) return;

            await _twitchService.SubscribeToAllTheStuffs(_eventSubWebsocketClient.SessionId);
            _logger.LogInformation("Subscribed to events");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.ConnectAsync();
            _logger.LogInformation("Websocket Connected.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }
    }
}
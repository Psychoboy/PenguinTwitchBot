using System.Diagnostics;
using DotNetTwitchBot.Bot.Core;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchWebsocketHostedService : IHostedService
    {
        private readonly ILogger<TwitchWebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        private TwitchService _twitchService;
        private ServiceBackbone _eventService;

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

            _eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            _eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            _twitchService = twitchService;
            _eventService = eventService;
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
            _logger.LogInformation("OnChannelSubscriptionRenewal: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            _logger.LogInformation("OnChannelSubscriptionGift: {0}", e.Notification.Payload.Event.UserName);
        }

        // private async void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        // {
        //     // await _eventService.OnSubscription(e.Notification.Payload.Event.);
        // }

        private async void onChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            _logger.LogInformation("onChannelSubscription: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private async void OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            _logger.LogInformation("OnChannelSubscriptionEnd: {0}", e.Notification.Payload.Event.UserName);
            await _eventService.OnSubscriptionEnd(e.Notification.Payload.Event.UserName);
        }

        private async void OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            await _eventService.OnCheer(e.Notification.Payload.Event);
        }

        private async void OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            await _eventService.OnChannelPointRedeem(
                e.Notification.Payload.Event.UserName,
                e.Notification.Payload.Event.Reward.Title,
                e.Notification.Payload.Event.UserInput);
            _logger.LogInformation("Channel pointed redeemed: {0}", e.Notification.Payload.Event.Reward.Title);
        }

        private async void OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
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
                var delayCounter = 1.0;
                while (!await _eventSubWebsocketClient.ConnectAsync())
                {
                    delayCounter = (delayCounter * 2);
                    if (delayCounter > 60.0) delayCounter = 60.0;
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
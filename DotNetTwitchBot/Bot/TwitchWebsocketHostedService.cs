using DotNetTwitchBot.Bot.Core;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace DotNetTwitchBot.Bot
{
    public class TwitchWebsocketHostedService : IHostedService
    {
        private readonly ILogger<TwitchWebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        private TwitchService _twitchService;
        private EventService _eventService;

        public TwitchWebsocketHostedService(
            ILogger<TwitchWebsocketHostedService> logger, 
            EventService eventService, 
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
            _eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionRenewal;
            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointRedeemed;
            _eventSubWebsocketClient.ChannelSubscriptionEnd += OnChannelSubscriptionEnd;

            _eventSubWebsocketClient.StreamOnline += OnStreamOnline;
            _eventSubWebsocketClient.StreamOffline += OnStreamOffline;
            _twitchService = twitchService;
            _eventService = eventService;
        }

        private void OnStreamOffline(object? sender, StreamOfflineArgs e)
        {
            _logger.LogInformation($"Stream is offline");
            _eventService.IsOnline = false;
        }

        private void OnStreamOnline(object? sender, StreamOnlineArgs e)
        {
            _logger.LogInformation($"Stream is online");
            _eventService.IsOnline = true;
        }

        private async void OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            await _eventService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private async void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            await _eventService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private async void onChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            await _eventService.OnSubscription(e.Notification.Payload.Event.UserName);   
        }
        
         private void OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            //TODO
        }

        private async void OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            await _eventService.OnCheer(e.Notification.Payload.Event.UserName);
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
            await _eventService.OnFollow(e.Notification.Payload.Event.UserName);
        }

        private void OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            _logger.LogError(e.Message);
        }

        private void OnWebsocketReconnected(object? sender, EventArgs e)
        {
            
        }

        private async void OnWebsocketDisconnected(object? sender, EventArgs e)
        {
            _logger.LogWarning("Websocket Disconnected");
            var delayCounter = 1.0;
            while(!await _eventSubWebsocketClient.ReconnectAsync()) {
                delayCounter = (delayCounter * 2);
                if(delayCounter > 60.0) delayCounter = 60.0;
                _logger.LogError("Websocket reconnected failed! Attempting again in {0} seconds.", delayCounter);
                await Task.Delay((int)delayCounter * 1000);                
            }
        }

        private async void OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            _logger.LogInformation("Websocket connected");
            if(e.IsRequestedReconnect) return;
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
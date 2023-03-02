using DotNetTwitchBot.Bot.Core;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace DotNetTwitchBot.Bot
{
    public class TwitchWebsocketHostedService : IHostedService
    {
        private readonly ILogger<TwitchWebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        private TwitchService _twitchService;
        private EventService _commandService;

        public TwitchWebsocketHostedService(ILogger<TwitchWebsocketHostedService> logger, EventService commandService, EventSubWebsocketClient eventSubWebsocketClient, TwitchService twitchService)
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
            _twitchService = twitchService;
            _commandService = commandService;
        }

        private async void OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            await _commandService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private async void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            await _commandService.OnSubscription(e.Notification.Payload.Event.UserName);
        }

        private async void onChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            await _commandService.OnSubscription(e.Notification.Payload.Event.UserName);   
        }

        private async void OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            await _commandService.OnCheer(e.Notification.Payload.Event.UserName);
        }

        private async void OnChannelPointRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            await _commandService.OnChannelPointRedeem(
                e.Notification.Payload.Event.UserName,
                e.Notification.Payload.Event.Reward.Title,
                e.Notification.Payload.Event.UserInput);
            _logger.LogInformation("Channel pointed redeemed: {0}", e.Notification.Payload.Event.Reward.Title);
        }

        private async void OnChannelFollow(object? sender, ChannelFollowArgs e)
        {
            await _commandService.OnFollow(e.Notification.Payload.Event.UserName);
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
            //TODO and Exponent attempt to not flood
            //await _eventSubWebsocketClient.ReconnectAsync();
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
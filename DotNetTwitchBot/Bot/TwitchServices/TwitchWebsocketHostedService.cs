using System.Collections.Concurrent;
using System.Diagnostics;
using DotNetTwitchBot.Bot.Core;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets.Core.Models;
using TwitchLib.EventSub.Core.Models.Subscriptions;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchWebsocketHostedService : IHostedService
    {
        private readonly ILogger<TwitchWebsocketHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        private ConcurrentBag<string> MessageIds = new ConcurrentBag<string>();
        private TwitchPubSub _twitchPubSub;
        private TwitchService _twitchService;
        private ServiceBackbone _eventService;
        private SubscriptionTracker _subscriptionHistory;
        private IConfiguration _configuration;
        private ConcurrentDictionary<string, DateTime> SubCache = new ConcurrentDictionary<string, DateTime>();
        static SemaphoreSlim _subscriptionLock = new SemaphoreSlim(1);

        public TwitchWebsocketHostedService(
            ILogger<TwitchWebsocketHostedService> logger,
            ILogger<TwitchPubSub> tbsLogger,
            ServiceBackbone eventService,
            IConfiguration configuration,
            EventSubWebsocketClient eventSubWebsocketClient,
            SubscriptionTracker subscriptionHistory,
            // TwitchPubSub twitchPubSub,
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
            _configuration = configuration;

            _twitchPubSub = new TwitchPubSub(tbsLogger);
            _twitchPubSub.OnPubSubServiceConnected += OnPubSubConnect;
            _twitchPubSub.OnPubSubServiceClosed += OnPubSubDisconnect;
            _twitchPubSub.OnChannelSubscription += OnPubSubSubscription;
            _twitchPubSub.OnListenResponse += OnPubSubListenResponse;

            _twitchService = twitchService;
            _eventService = eventService;
            _subscriptionHistory = subscriptionHistory;
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
                _logger.LogWarning("Already processed message: {0} - {1} - {2}", metadata.MessageId, metadata.MessageType, metadata.MessageTimestamp);
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

        private async void onChannelSubscription(object? sender, ChannelSubscribeArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("onChannelSubscription: {0} -- IsGift?: {1} Type: {2} Tier- {3}"
            , e.Notification.Payload.Event.UserLogin, e.Notification.Payload.Event.IsGift, e.Notification.Metadata.SubscriptionType, e.Notification.Payload.Event.Tier);

            if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;
            if (await CheckIfPreviousSub(e.Notification.Payload.Event.UserLogin))
            {
                _logger.LogInformation("{0} previously subscribed, waiting for Renewal.", e.Notification.Payload.Event.UserLogin);
                return;
            }
            await _eventService.OnSubscription(new Events.SubscriptionEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                IsGift = e.Notification.Payload.Event.IsGift
            });
            await _subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin);
        }

        private Task<bool> CheckIfPreviousSub(string userLogin)
        {
            return _subscriptionHistory.ExistingSub(userLogin);
        }

        private async void OnChannelSubscriptionRenewal(object? sender, ChannelSubscriptionMessageArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelSubscriptionRenewal: {0}", e.Notification.Payload.Event.UserLogin);

            if (CheckIfExistsAndAddSubCache(e.Notification.Payload.Event.UserLogin)) return;
            await _eventService.OnSubscription(new Events.SubscriptionEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                Count = e.Notification.Payload.Event.CumulativeMonths,
                IsRenewal = true,
                Message = e.Notification.Payload.Event.Message?.Text
            });
            await _subscriptionHistory.AddOrUpdateSubHistory(e.Notification.Payload.Event.UserLogin);
        }

        private async void OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelSubscriptionGift: {0}", e.Notification.Payload.Event.UserLogin);
            await _eventService.OnSubscriptionGift(new Events.SubscriptionGiftEventArgs
            {
                Name = e.Notification.Payload.Event.UserLogin,
                DisplayName = e.Notification.Payload.Event.UserName,
                GiftAmount = e.Notification.Payload.Event.Total,
                TotalGifted = e.Notification.Payload.Event.CumulativeTotal
            });
        }

        private async void OnPubSubSubscription(object? sender, OnChannelSubscriptionArgs e)
        {
            _logger.LogInformation("Pub Sub Subscription {0} {1} Months: {2} IsGift: {3}", e.Subscription.DisplayName, e.Subscription.Username, e.Subscription.CumulativeMonths, e.Subscription.IsGift);
            if (CheckIfExistsAndAddSubCache(e.Subscription.Username)) return;
            await _eventService.OnSubscription(new Events.SubscriptionEventArgs
            {
                Name = e.Subscription.Username,
                DisplayName = e.Subscription.DisplayName,
                IsGift = e.Subscription.IsGift != null ? (bool)e.Subscription.IsGift : false,
                IsRenewal = e.Subscription.Months > 0,
                Count = e.Subscription.Months,
                Message = e.Subscription.SubMessage?.Message
            });
            await _subscriptionHistory.AddOrUpdateSubHistory(e.Subscription.Username);
        }


        private async void OnChannelSubscriptionEnd(object? sender, ChannelSubscriptionEndArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;

            _logger.LogInformation("OnChannelSubscriptionEnd: {0} Type: {1}", e.Notification.Payload.Event.UserLogin, e.Notification.Metadata.SubscriptionType);
            await _eventService.OnSubscriptionEnd(e.Notification.Payload.Event.UserLogin);
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
                if (SubCache.ContainsKey(name) && SubCache[name] > DateTime.Now.AddDays(-5))
                {
                    _logger.LogWarning("Subscriber already in sub cache");
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

        private async void OnChannelCheer(object? sender, ChannelCheerArgs e)
        {
            if (DidProcessMessage(e.Notification.Metadata)) return;
            _logger.LogInformation("OnChannelCheer: {0}", e.Notification.Payload.Event.UserLogin);
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
            _logger.LogInformation("OnChannelFollow: {0}", e.Notification.Payload.Event.UserLogin);
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
                var delayCounter = 1;
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    delayCounter = (delayCounter * 2);
                    if (delayCounter > 60) delayCounter = 60;
                    _logger.LogError("Websocket reconnected failed! Attempting again in {0} seconds.", delayCounter);
                    await Task.Delay(delayCounter * 1000);
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
                while (!await _eventSubWebsocketClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws")))
                {
                    delayCounter = (delayCounter * 2);
                    if (delayCounter > 300)
                    {
                        return;
                    }
                    await Task.Delay(delayCounter * 1000);
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
            await _eventSubWebsocketClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"));
            //await _eventSubWebsocketClient.ConnectAsync(new Uri("http://localhost:8080/eventsub/subscription"));
            _logger.LogInformation("Websocket Connected.");
            try
            {

                _twitchPubSub.ListenToSubscriptions(await _twitchService.GetBroadcasterUserId());
                _twitchPubSub.Connect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to twitchpub");
            }
        }

        private void OnPubSubListenResponse(object? sender, OnListenResponseArgs e)
        {
            _logger.LogInformation("PubSub Listen Successful: {0} Error {1} Topic: {2}", e.Response.Successful, e.Response.Error, e.Topic);
        }

        private void OnPubSubConnect(object? sender, EventArgs e)
        {
            _logger.LogInformation("PubSub Connected");
            _twitchPubSub.SendTopics(_configuration["twitchAccessToken"]);
        }

        private void OnPubSubDisconnect(object? sender, EventArgs e)
        {
            _logger.LogInformation("PubSub Disconnected");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }
    }
}
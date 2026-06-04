using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Stream;
using PenguinTwitchBot.TwitchApi.EventSub.Models;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Stream;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Extensions;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Models;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets
{
    public class EventSubWebsocketClient
    {
        public event AsyncEventHandler<WebsocketConnectedEventArgs>? WebsocketConnected;
        public event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event AsyncEventHandler<WebsocketDisconnectedEventArgs>? WebsocketDisconnected;
        public event AsyncEventHandler<ErrorOccuredEventArgs>? ErrorOccurred;
        public event AsyncEventHandler<WebsocketReconnectedEventArgs>? WebsocketReconnected;

        public event AsyncEventHandler<ChannelAdBreakBeginEventArgs>? ChannelAdBreakBegin;
        public event AsyncEventHandler<ChannelBanEventArgs>? ChannelBan;
        public event AsyncEventHandler<ChannelBitsUseEventArgs>? ChannelBitsUse;
        public event AsyncEventHandler<ChannelChatMessageEventArgs>? ChannelChatMessage;
        public event AsyncEventHandler<ChannelChatMessageDeleteEventArgs>? ChannelChatMessageDelete;
        public event AsyncEventHandler<ChannelChatNotificationEventArgs>? ChannelChatNotification;
        public event AsyncEventHandler<ChannelCheerEventArgs>? ChannelCheer;
        public event AsyncEventHandler<ChannelFollowEventArgs>? ChannelFollow;
        public event AsyncEventHandler<ChannelPointsCustomRewardRedemptionEventArgs>? ChannelPointsCustomRewardRedemptionAdd;
        public event AsyncEventHandler<ChannelPointsCustomRewardRedemptionEventArgs>? ChannelPointsCustomRewardRedemptionUpdate;
        public event AsyncEventHandler<ChannelRaidEventArgs>? ChannelRaid;
        public event AsyncEventHandler<ChannelSubscribeEventArgs>? ChannelSubscribe;
        public event AsyncEventHandler<ChannelSubscriptionEndEventArgs>? ChannelSubscriptionEnd;
        public event AsyncEventHandler<ChannelSubscriptionGiftEventArgs>? ChannelSubscriptionGift;
        public event AsyncEventHandler<ChannelSubscriptionMessageEventArgs>? ChannelSubscriptionMessage;
        public event AsyncEventHandler<ChannelSuspiciousUserMessageEventArgs>? ChannelSuspiciousUserMessage;
        public event AsyncEventHandler<ChannelUnbanEventArgs>? ChannelUnban;
        public event AsyncEventHandler<StreamOnlineEventArgs>? StreamOnline;
        public event AsyncEventHandler<StreamOfflineEventArgs>? StreamOffline;

        public string SessionId { get; private set; } = string.Empty;
        private CancellationTokenSource? _cts;
        private DateTimeOffset _lastReceived = DateTimeOffset.MinValue;
        private TimeSpan _keepAliveTimeout = TimeSpan.Zero;
        private bool _reconnectRequested = false;
        private bool _reconnectComplete = false;
        private int _websocketDisconnectedInvoked = 0;
        private WebsocketClient _websocketClient;
        private readonly ILogger<EventSubWebsocketClient> _logger;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IServiceProvider? _serviceProvider;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private const string WEBSOCKET_URL = "wss://eventsub.wss.twitch.tv/ws";

        public EventSubWebsocketClient(ILogger<EventSubWebsocketClient> logger, IServiceProvider serviceProvider, WebsocketClient websocketClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _websocketClient = websocketClient ?? throw new ArgumentNullException(nameof(websocketClient));
            _websocketClient.OnDataReceived += OnDataReceived;
            _websocketClient.OnErrorOccurred += OnErrorOccurred;

            _reconnectComplete = false;
            _reconnectRequested = false;
        }

        public EventSubWebsocketClient(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

            _logger = _loggerFactory.CreateLogger<EventSubWebsocketClient>();
            _websocketClient = new WebsocketClient(_loggerFactory.CreateLogger<WebsocketClient>());

            _websocketClient.OnDataReceived += OnDataReceived;
            _websocketClient.OnErrorOccurred += OnErrorOccurred;

            _reconnectComplete = false;
            _reconnectRequested = false;
        }

        public async Task<bool> ConnectAsync(Uri? url = null)
        {
            url ??= new Uri(WEBSOCKET_URL);
            _lastReceived = DateTimeOffset.MinValue;

            var wasConnected = _websocketClient.IsConnected;

            var success = await _websocketClient.ConnectAsync(url).ConfigureAwait(false);

            if (!success)
                return false;

            Interlocked.Exchange(ref _websocketDisconnectedInvoked, 0);

            var monitorRequired = !wasConnected && _websocketClient.IsConnected;
            if (!monitorRequired)
                return true;

            _cts?.Cancel();

            _cts = new CancellationTokenSource();
            var connectionCheckToken = _cts.Token;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Factory.StartNew(() => ConnectionCheckAsync(connectionCheckToken), connectionCheckToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            return true;
        }

        public async Task<bool> DisconnectAsync()
        {
            _cts?.Cancel();
            return await _websocketClient.DisconnectAsync().ConfigureAwait(false);
        }

        public Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
        {
            return ReconnectAsync(new Uri(WEBSOCKET_URL), cancellationToken);
        }

        private async Task<bool> ReconnectAsync(Uri url, CancellationToken cancellationToken = default)
        {
            url ??= new Uri(WEBSOCKET_URL);

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (_reconnectRequested)
            {

                var reconnectClient = _serviceProvider != null
                    ? _serviceProvider.GetRequiredService<WebsocketClient>()
                    : new WebsocketClient((_loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<WebsocketClient>());

                reconnectClient.OnDataReceived += OnDataReceived;
                reconnectClient.OnErrorOccurred += OnErrorOccurred;

                if (!await reconnectClient.ConnectAsync(url))
                    return false;


                for (var i = 0; i < 200; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (_reconnectComplete)
                    {
                        var oldRunningClient = _websocketClient;
                        _websocketClient = reconnectClient;

                        if (oldRunningClient.IsConnected)
                            await oldRunningClient.DisconnectAsync();
                        oldRunningClient.Dispose();

                        await WebsocketReconnected.InvokeAsync(this, new());

                        _reconnectRequested = false;
                        _reconnectComplete = false;

                        return true;
                    }

                    try
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                reconnectClient.OnDataReceived -= OnDataReceived;
                reconnectClient.OnErrorOccurred -= OnErrorOccurred;

                if (reconnectClient.IsConnected)
                    await reconnectClient.DisconnectAsync();

                reconnectClient.Dispose();

                _logger.LogReconnectFailed(SessionId);

                return false;
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (_websocketClient.IsConnected)
                await DisconnectAsync();

            _websocketClient.Dispose();

            _websocketClient = _serviceProvider != null
                ? _serviceProvider.GetRequiredService<WebsocketClient>()
                : new WebsocketClient((_loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<WebsocketClient>());

            _websocketClient.OnDataReceived += OnDataReceived;
            _websocketClient.OnErrorOccurred += OnErrorOccurred;

            if(!_websocketClient.IsConnected)
                if (!await ConnectAsync())
                    return false;

            await WebsocketReconnected.InvokeAsync(this, new());

            return true;
        }

        private async Task ConnectionCheckAsync(CancellationToken cancellationToken)
        {
            while (_websocketClient.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                if (_lastReceived != DateTimeOffset.MinValue)
                    if (_keepAliveTimeout != TimeSpan.Zero)
                        if (_lastReceived.Add(_keepAliveTimeout) < DateTimeOffset.Now)
                            break;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            await DisconnectAsync();

            if (!cancellationToken.IsCancellationRequested && Interlocked.CompareExchange(ref _websocketDisconnectedInvoked, 1, 0) == 0)
                await WebsocketDisconnected.InvokeAsync(this, new());
        }

        private async Task OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            _logger.LogMessage(e.Bytes);
            _lastReceived = DateTimeOffset.Now;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => await MessageReceived.InvokeAsync(this, new MessageReceivedEventArgs()));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            var json = JsonDocument.Parse(e.Bytes);
            var metadata = json.RootElement.GetProperty("metadata"u8).Deserialize<WebsocketEventSubMetaData>(_jsonSerializerOptions)!;
            var payload = json.RootElement.GetProperty("payload"u8);
            try
            {
                switch(metadata.MessageType)
                {
                    case "session_welcome":
                        await HandleWelcomeAsync(metadata, payload);
                        break;
                    case "session_disconnect":
                        await HandleDisconnectAsync(metadata, payload);
                        break;
                    case "session_reconnect":
                        HandleReconnect(metadata, payload);
                        break;
                    case "session_keepalive":
                        HandleKeepAlive(metadata, payload);
                        break;
                    case "notification":
                        await HandleNotificationAsync(metadata, payload);
                        break;
                    case "revocation":
                        await HandleRevocationAsync(metadata, payload);
                        break;
                    default:
                        _logger.LogUnknownMessageType(metadata.MessageType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing EventSub notification.");
            }
        }

        private async Task OnErrorOccurred(object? sender, ErrorOccuredEventArgs e)
        {
            await ErrorOccurred.InvokeAsync(this, e);
        }

        private void HandleReconnect(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            _ = metadata;
            _logger.LogReconnectRequested(SessionId);
            var data = JsonSerializer.Deserialize<EventSubWebsocketSessionInfoPayload>(payload, _jsonSerializerOptions);
            _reconnectRequested = true;
            Task.Run(async () =>
            {
               await ReconnectAsync(new Uri(data?.Session.ReconnectUrl ?? WEBSOCKET_URL));
            });
        }

        private async ValueTask HandleWelcomeAsync(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            _ = metadata;
            var data = JsonSerializer.Deserialize<EventSubWebsocketSessionInfoPayload>(payload, _jsonSerializerOptions);
           
            if(data is null)
                return;

            if(_reconnectRequested)
                _reconnectComplete = true;

            Interlocked.Exchange(ref _websocketDisconnectedInvoked, 0);

            SessionId = data.Session.Id;
            var keepAliveTimeout = data.Session.KeepaliveTimeoutSeconds + data.Session.KeepaliveTimeoutSeconds * 0.2;
            _keepAliveTimeout = TimeSpan.FromSeconds(keepAliveTimeout ?? 10);
            await WebsocketConnected.InvokeAsync(this, new WebsocketConnectedEventArgs { IsRequestedReconnect = _reconnectRequested, KeepAliveTimeout = _keepAliveTimeout });
        }

        private async Task HandleDisconnectAsync(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            _ = metadata;
            var data = JsonSerializer.Deserialize<EventSubWebsocketSessionInfoPayload>(payload, _jsonSerializerOptions);
            if(data != null)
                _logger.LogForceDisconnected(SessionId, data.Session.DisconnectedAt, data.Session.DisconnectReason);

            if (Interlocked.CompareExchange(ref _websocketDisconnectedInvoked, 1, 0) == 0)
                await WebsocketDisconnected.InvokeAsync(this, new ());
        }

        private void HandleKeepAlive(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            _ = metadata;
            _ = payload;
        }

        private async Task HandleNotificationAsync(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            if(!metadata.HasSubscriptionInfo)
            {
                await ErrorOccurred.InvokeAsync(this, 
                new ErrorOccuredEventArgs { Message = "Received notification without subscription info.", Exception = new InvalidOperationException("Received notification without subscription info.") });
                return;
            }

            var task = (metadata.SubscriptionType, metadata.SubscriptionVersion) switch
            {
                ("channel.ad_break.begin", "1") => InvokeEventSubEvent<ChannelAdBreakBeginEventArgs, ChannelAdBreakBegin>(ChannelAdBreakBegin),
                ("channel.ban", "1") => InvokeEventSubEvent<ChannelBanEventArgs, ChannelBan>(ChannelBan),
                ("channel.bits.use", "1") => InvokeEventSubEvent<ChannelBitsUseEventArgs, ChannelBitsUse>(ChannelBitsUse),
                ("channel.chat.message", "1") => InvokeEventSubEvent<ChannelChatMessageEventArgs, ChannelChatMessage>(ChannelChatMessage),
                ("channel.chat.message_delete", "1") => InvokeEventSubEvent<ChannelChatMessageDeleteEventArgs, ChannelChatMessageDelete>(ChannelChatMessageDelete),
                ("channel.chat.notification", "1") => InvokeEventSubEvent<ChannelChatNotificationEventArgs, ChannelChatNotification>(ChannelChatNotification),
                ("channel.cheer", "1") => InvokeEventSubEvent<ChannelCheerEventArgs, ChannelCheer>(ChannelCheer),
                ("channel.follow", "2") => InvokeEventSubEvent<ChannelFollowEventArgs, ChannelFollow>(ChannelFollow),
                ("channel.channel_points_custom_reward_redemption.add", "1") => InvokeEventSubEvent<ChannelPointsCustomRewardRedemptionEventArgs, ChannelPointsCustomRewardRedemption>(ChannelPointsCustomRewardRedemptionAdd),
                ("channel.channel_points_custom_reward_redemption.update", "1") => InvokeEventSubEvent<ChannelPointsCustomRewardRedemptionEventArgs, ChannelPointsCustomRewardRedemption>(ChannelPointsCustomRewardRedemptionUpdate),
                ("channel.raid", "1") => InvokeEventSubEvent<ChannelRaidEventArgs, ChannelRaid>(ChannelRaid),
                ("channel.subscribe", "1") => InvokeEventSubEvent<ChannelSubscribeEventArgs, ChannelSubscribe>(ChannelSubscribe),
                ("channel.subscription.end", "1") => InvokeEventSubEvent<ChannelSubscriptionEndEventArgs, ChannelSubscriptionEnd>(ChannelSubscriptionEnd),
                ("channel.subscription.gift", "1") => InvokeEventSubEvent<ChannelSubscriptionGiftEventArgs, ChannelSubscriptionGift>(ChannelSubscriptionGift),
                ("channel.subscription.message", "1") => InvokeEventSubEvent<ChannelSubscriptionMessageEventArgs, ChannelSubscriptionMessage>(ChannelSubscriptionMessage),
                ("channel.suspicious_user.message", "1") => InvokeEventSubEvent<ChannelSuspiciousUserMessageEventArgs, ChannelSuspiciousUserMessage>(ChannelSuspiciousUserMessage),
                ("channel.unban", "1") => InvokeEventSubEvent<ChannelUnbanEventArgs, ChannelUnban>(ChannelUnban),
                ("stream.online", "1") => InvokeEventSubEvent<StreamOnlineEventArgs, StreamOnline>(StreamOnline),
                ("stream.offline", "1") => InvokeEventSubEvent<StreamOfflineEventArgs, StreamOffline>(StreamOffline),
                _ => Task.CompletedTask,
            };
            await task;

            async Task InvokeEventSubEvent<TEvent, TModel>(AsyncEventHandler<TEvent>? asyncEventHandler)
                where TEvent : EventSubEventArgs<TModel>
            {
                var notification = JsonSerializer.Deserialize<EventSubNotificationPayload<TModel>>(payload, _jsonSerializerOptions);
                var eventArgs = Activator.CreateInstance<TEvent>();
                eventArgs.Metadata = metadata;
                eventArgs.Event = notification!.Event;
                await asyncEventHandler.InvokeAsync(this, eventArgs);
            }
        }

        private Task HandleRevocationAsync(WebsocketEventSubMetaData metadata, JsonElement payload)
        {
            _ = payload;
            _logger.LogInformation("Subscription revoked for session {SessionId}, type {SubscriptionType}.", SessionId, metadata.SubscriptionType);
            return Task.CompletedTask;
        }
    }
}
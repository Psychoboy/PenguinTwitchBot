using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Stream;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets
{
    public interface IEventSubWebsocketClient
    {
        string SessionId { get; }
        Task<bool> ConnectAsync();
        Task<bool> ConnectAsync(Uri? url);
        Task<bool> DisconnectAsync();
        Task<bool> ReconnectAsync();
        Task<bool> ReconnectAsync(CancellationToken cancellationToken);

        event AsyncEventHandler<WebsocketConnectedEventArgs>? WebsocketConnected;
        event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
        event AsyncEventHandler<WebsocketDisconnectedEventArgs>? WebsocketDisconnected;
        event AsyncEventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
        event AsyncEventHandler<WebsocketReconnectedEventArgs>? WebsocketReconnected;
        event AsyncEventHandler<ChannelAdBreakBeginEventArgs>? ChannelAdBreakBegin;
        event AsyncEventHandler<ChannelBanEventArgs>? ChannelBan;
        event AsyncEventHandler<ChannelBitsUseEventArgs>? ChannelBitsUse;
        event AsyncEventHandler<ChannelChatMessageEventArgs>? ChannelChatMessage;
        event AsyncEventHandler<ChannelChatMessageDeleteEventArgs>? ChannelChatMessageDelete;
        event AsyncEventHandler<ChannelChatNotificationEventArgs>? ChannelChatNotification;
        event AsyncEventHandler<ChannelCheerEventArgs>? ChannelCheer;
        event AsyncEventHandler<ChannelFollowEventArgs>? ChannelFollow;
        event AsyncEventHandler<ChannelPointsCustomRewardRedemptionEventArgs>? ChannelPointsCustomRewardRedemptionAdd;
        event AsyncEventHandler<ChannelRaidEventArgs>? ChannelRaid;
        event AsyncEventHandler<ChannelSubscribeEventArgs>? ChannelSubscribe;
        event AsyncEventHandler<ChannelSubscriptionEndEventArgs>? ChannelSubscriptionEnd;
        event AsyncEventHandler<ChannelSubscriptionGiftEventArgs>? ChannelSubscriptionGift;
        event AsyncEventHandler<ChannelSubscriptionMessageEventArgs>? ChannelSubscriptionMessage;
        event AsyncEventHandler<ChannelSuspiciousUserMessageEventArgs>? ChannelSuspiciousUserMessage;
        event AsyncEventHandler<ChannelUnbanEventArgs>? ChannelUnban;
        event AsyncEventHandler<StreamOnlineEventArgs>? StreamOnline;
        event AsyncEventHandler<StreamOfflineEventArgs>? StreamOffline;
    }
}

using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.CustomMiddleware;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IServiceBackbone
    {
        string? BotName { get; set; }
        string BroadcasterName { get; }
        bool IsOnline { get; set; }
        bool HealthStatus { get; }

        event AsyncEventHandler<AdBreakStartEventArgs>? AdBreakStartEvent;
        event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        event AsyncEventHandler<RaidEventArgs>? IncomingRaidEvent;
        event AsyncEventHandler? StreamEnded;
        event AsyncEventHandler? StreamStarted;
        event AsyncEventHandler<SubscriptionEndEventArgs>? SubscriptionEndEvent;
        event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        event AsyncEventHandler<SubscriptionGiftEventArgs>? SubscriptionGiftEvent;
        event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;
        event AsyncEventHandler<BanEventArgs>? BanEvent;

        bool IsBroadcasterOrBot(string name);
        bool IsKnownBot(string name);
        bool IsKnownBotOrCurrentStreamer(string name);
        Task OnAdBreakStartEvent(AdBreakStartEventArgs e);
        Task OnChannelPointRedeem(string userId, string userName, string title);
        Task OnChannelPointRedeem(string userId, string userName, string id, string userInput);
        //Task OnChatMessage(ChatMessageEventArgs message);
        Task OnCheer(ChannelCheer ev);
        Task OnCommand(CommandEventArgs command);
        Task OnFollow(ChannelFollow ev);
        Task OnIncomingRaid(RaidEventArgs args);
        Task OnStreamEnded();
        Task OnStreamStarted();
        Task OnSubscription(SubscriptionEventArgs eventArgs);
        Task OnSubscriptionEnd(string userName, string userId);
        Task OnSubscriptionGift(SubscriptionGiftEventArgs eventArgs);
        Task OnUserJoined(string username);
        Task OnUserLeft(string username);
        Task OnWhisperCommand(CommandEventArgs command);
        Task RunCommand(CommandEventArgs eventArgs);
        Task SendChatMessage(string message);
        Task SendChatMessage(string name, string message);
        Task SendChatMessageWithTitle(string viewerName, string message);
        Task OnViewerBan(string userId, string username, bool unbanned, DateTimeOffset? endsAt);
        Task ResponseWithMessage(CommandEventArgs e, string message);
        Task SendChatMessage(string message, PlatformType platform);
        Task ResponseWithMessage(CommandEventArgs e, string message, PlatformType platform);
        Task SendChatMessage(string name, string message, PlatformType platform);
        Task SendChatMessageWithTitle(string viewerName, string message, PlatformType platform);
    }
}
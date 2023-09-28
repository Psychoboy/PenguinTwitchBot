using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IServiceBackbone
    {
        string? BotName { get; set; }
        string BroadcasterName { get; }
        bool IsOnline { get; set; }
        bool HealthStatus { get; }

        event ServiceBackbone.AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        event ServiceBackbone.AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        event ServiceBackbone.AsyncEventHandler<CheerEventArgs>? CheerEvent;
        event ServiceBackbone.AsyncEventHandler<CommandEventArgs>? CommandEvent;
        event ServiceBackbone.AsyncEventHandler<FollowEventArgs>? FollowEvent;
        event ServiceBackbone.AsyncEventHandler<RaidEventArgs>? IncomingRaidEvent;
        event ServiceBackbone.AsyncEventHandler<string>? SendMessageEvent;
        event ServiceBackbone.AsyncEventHandler<string, string>? SendWhisperMessageEvent;
        event ServiceBackbone.AsyncEventHandler? StreamEnded;
        event ServiceBackbone.AsyncEventHandler? StreamStarted;
        event ServiceBackbone.AsyncEventHandler<SubscriptionEndEventArgs>? SubscriptionEndEvent;
        event ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        event ServiceBackbone.AsyncEventHandler<SubscriptionGiftEventArgs>? SubscriptionGiftEvent;
        event ServiceBackbone.AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        event ServiceBackbone.AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;
        event ServiceBackbone.AsyncEventHandler<BanEventArgs>? BanEvent;

        bool IsBroadcasterOrBot(string name);
        bool IsKnownBot(string name);
        bool IsKnownBotOrCurrentStreamer(string name);
        Task OnChannelPointRedeem(string userName, string title, string userInput);
        Task OnChatMessage(ChatMessageEventArgs message);
        Task OnCheer(ChannelCheer ev);
        Task OnCommand(CommandEventArgs command);
        Task OnFollow(ChannelFollow ev);
        Task OnIncomingRaid(RaidEventArgs args);
        Task OnStreamEnded();
        Task OnStreamStarted();
        Task OnSubscription(SubscriptionEventArgs eventArgs);
        Task OnSubscriptionEnd(string userName);
        Task OnSubscriptionGift(SubscriptionGiftEventArgs eventArgs);
        Task OnUserJoined(string username);
        Task OnUserLeft(string username);
        Task OnWhisperCommand(CommandEventArgs command);
        Task RunCommand(CommandEventArgs eventArgs);
        Task SendChatMessage(string message);
        Task SendChatMessage(string name, string message);
        Task SendChatMessageWithTitle(string viewerName, string message);
        Task SendWhisperMessage(string name, string message);
        Task OnViewerBan(string username, bool unbanned);
    }
}
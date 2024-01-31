using DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Core.SubscriptionTypes.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Websockets.Core.EventArgs.Channel
{
    public class ChannelChatMessageArgs : TwitchEventSubEventArgs<EventSubNotification<ChannelChatMessage>>
    {
    }
}

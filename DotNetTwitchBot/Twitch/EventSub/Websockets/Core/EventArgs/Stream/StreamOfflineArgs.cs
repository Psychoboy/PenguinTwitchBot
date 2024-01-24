using DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Stream;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs.Stream
{
    public class StreamOfflineArgs : TwitchEventSubEventArgs<EventSubNotification<StreamOffline>>
    { }
}
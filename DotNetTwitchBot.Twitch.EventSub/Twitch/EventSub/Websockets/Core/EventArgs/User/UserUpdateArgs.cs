using DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.User;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs.User
{
    public class UserUpdateArgs : TwitchEventSubEventArgs<EventSubNotification<UserUpdate>>
    { }
}
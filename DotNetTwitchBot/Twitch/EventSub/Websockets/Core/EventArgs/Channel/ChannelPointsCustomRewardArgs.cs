﻿using DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs.Channel
{
    public class ChannelPointsCustomRewardArgs : TwitchEventSubEventArgs<EventSubNotification<ChannelPointsCustomReward>>
    { }
}
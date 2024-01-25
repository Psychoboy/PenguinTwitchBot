using DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Handler;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;
using System.Text.Json;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Handler.Channel.Ads
{
    public class AdBreakBeginHandler : INotificationHandler
    {
        /// <inheritdoc />
        public string SubscriptionType => "channel.ad_break.begin";

        /// <inheritdoc />
        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            try
            {
                var data = JsonSerializer.Deserialize<EventSubNotification<ChannelAdBreakBegin>>(jsonString.AsSpan(), serializerOptions);
                if (data is null)
                    throw new InvalidOperationException("Parsed JSON cannot be null!");
                client.RaiseEvent("ChannelAdBreakBegin", new ChannelAdBreakBeginArgs { Notification = data });
            }
            catch (Exception ex)
            {
                client.RaiseEvent("ErrorOccurred", new ErrorOccuredArgs { Exception = ex, Message = $"Error encountered while trying to handle {SubscriptionType} notification! Raw Json: {jsonString}" });
            }
        }
    }
}

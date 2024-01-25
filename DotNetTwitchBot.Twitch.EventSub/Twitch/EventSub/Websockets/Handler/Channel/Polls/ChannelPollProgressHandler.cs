using System;
using System.Text.Json;
using DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Handler;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Handler.Channel.Polls
{
    /// <summary>
    /// Handler for 'channel.poll.progress' notifications
    /// </summary>
    public class ChannelPollProgressHandler : INotificationHandler
    {
        /// <inheritdoc />
        public string SubscriptionType => "channel.poll.progress";

        /// <inheritdoc />
        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            try
            {
                var data = JsonSerializer.Deserialize<EventSubNotification<ChannelPollProgress>>(jsonString.AsSpan(), serializerOptions);

                if (data is null)
                    throw new InvalidOperationException("Parsed JSON cannot be null!");

                client.RaiseEvent("ChannelPollProgress", new ChannelPollProgressArgs { Notification = data });
            }
            catch (Exception ex)
            {
                client.RaiseEvent("ErrorOccurred", new ErrorOccuredArgs { Exception = ex, Message = $"Error encountered while trying to handle {SubscriptionType} notification! Raw Json: {jsonString}" });
            }
        }
    }
}
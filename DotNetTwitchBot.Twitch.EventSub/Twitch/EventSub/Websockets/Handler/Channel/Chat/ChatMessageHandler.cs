using DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Core.SubscriptionTypes.Channel;
using DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Websockets.Core.EventArgs.Channel;
using DotNetTwitchBot.Twitch.EventSub.Websockets;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Handler;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;
using System.Text.Json;

namespace DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Websockets.Handler.Channel.Chat
{
    public class ChatMessageHandler : INotificationHandler
    {
        public string SubscriptionType => "channel.chat.message";

        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            try
            {
                var data = JsonSerializer.Deserialize<EventSubNotification<ChannelChatMessage>>(jsonString.AsSpan(), serializerOptions);
                if (data is null)
                    throw new InvalidOperationException("Parsed JSON cannot be null!");
                client.RaiseEvent("ChannelChatMessage", new ChannelChatMessageArgs { Notification = data });
            }
            catch (Exception ex)
            {
                client.RaiseEvent("ErrorOccurred", new ErrorOccuredArgs { Exception = ex, Message = $"Error encountered while trying to handle {SubscriptionType} notification! Raw Json: {jsonString}" });
            }
        }
    }
}

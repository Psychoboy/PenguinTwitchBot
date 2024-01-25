using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Handler;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Models;
using System.Text.Json;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Handler
{
    /// <summary>
    /// Handler for 'revocation' notifications
    /// </summary>
    public class RevocationHandler : INotificationHandler
    {
        /// <inheritdoc />
        public string SubscriptionType => "revocation";

        /// <inheritdoc />
        public void Handle(EventSubWebsocketClient client, string jsonString, JsonSerializerOptions serializerOptions)
        {
            var data = JsonSerializer.Deserialize<EventSubNotification<object>>(jsonString.AsSpan(), serializerOptions);

            if (data is null)
                throw new InvalidOperationException("Parsed JSON cannot be null!");

            client.RaiseEvent("Revocation", new RevocationArgs { Notification = data });
        }
    }
}

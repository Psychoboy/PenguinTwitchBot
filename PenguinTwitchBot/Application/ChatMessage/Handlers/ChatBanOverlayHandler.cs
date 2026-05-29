using System.Text.Json;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.ChatMessage.Handlers
{
    /// <summary>
    /// Notifies overlay clients that all messages from a banned/timed-out user should be removed.
    /// </summary>
    public class ChatBanOverlayHandler(IWebSocketMessenger webSocketMessenger)
        : Application.Notifications.INotificationHandler<BannedChatUser>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task Handle(BannedChatUser request, CancellationToken cancellationToken)
        {
            var payload = new { type = "chat_user_banned", userId = request.UserId };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await webSocketMessenger.AddToQueue(json);
        }
    }
}

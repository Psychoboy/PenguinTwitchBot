using System.Text.Json;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.ChatMessage.Handlers
{
    /// <summary>
    /// Notifies overlay clients that a specific chat message was deleted.
    /// </summary>
    public class ChatDeleteOverlayHandler(IWebSocketMessenger webSocketMessenger)
        : Application.Notifications.INotificationHandler<DeletedChatMessage>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task Handle(DeletedChatMessage request, CancellationToken cancellationToken)
        {
            var messageId = request.EventArgs.Event.MessageId;
            var payload = new { type = "chat_delete", id = messageId };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await webSocketMessenger.AddToQueue(json);
        }
    }
}

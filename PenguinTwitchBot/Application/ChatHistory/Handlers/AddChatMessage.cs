using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Application.ChatHistory.Handlers
{
    public class AddChatMessage(IChatHistory chatHistory) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return chatHistory.AddChatMessage(notification.EventArgs);
        }
    }
}

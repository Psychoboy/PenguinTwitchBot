using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Application.ChatHistory.Handlers
{
    public class DeleteChatMessage(IChatHistory chatHistory) : Application.Notifications.INotificationHandler<DeletedChatMessage>
    {
        public Task Handle(DeletedChatMessage notification, CancellationToken cancellationToken)
        {

            return chatHistory.DeleteChatMessage(notification.EventArgs);
        }
    }
}

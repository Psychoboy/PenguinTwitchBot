using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Application.ChatHistory.Handlers
{
    public class DeleteChatMessage(IChatHistory chatHistory) : Application.Notifications.INotificationHandler<DeletedChatMessage>
    {
        public Task Handle(DeletedChatMessage notification, CancellationToken cancellationToken)
        {

            return chatHistory.DeleteChatMessage(notification.EventArgs);
        }
    }
}

using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Application.ChatHistory.Handlers
{
    public class AddChatMessage(IChatHistory chatHistory) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return chatHistory.AddChatMessage(notification.EventArgs);
        }
    }
}

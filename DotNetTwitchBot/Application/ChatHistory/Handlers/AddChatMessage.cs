using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.ChatHistory.Handlers
{
    public class AddChatMessage(IChatHistory chatHistory) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return chatHistory.AddChatMessage(notification.EventArgs);
        }
    }
}

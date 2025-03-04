using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Application.ChatHistory.Handlers
{
    public class DeleteChatMessage(IChatHistory chatHistory) : INotificationHandler<DeletedChatMessage>
    {
        public Task Handle(DeletedChatMessage notification, CancellationToken cancellationToken)
        {
            return chatHistory.DeleteChatMessage(notification.EventArgs);
        }
    }
}

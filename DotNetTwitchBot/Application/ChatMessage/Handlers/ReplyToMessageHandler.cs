using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Application.ChatMessage.Handlers
{
    public class ReplyToMessageHandler(ITwitchChatBot chatBot) : Application.Notifications.INotificationHandler<ReplyToMessage>
    {
        public Task Handle(ReplyToMessage request, CancellationToken cancellationToken)
        {
            return chatBot.ReplyToMessage(request.Name, request.MessageId, request.Message);
        }
    }
}

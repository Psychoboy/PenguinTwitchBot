using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Application.ChatMessage.Handlers
{
    public class SendBotMessageHandler(ITwitchChatBot chatBot) : Application.Notifications.INotificationHandler<SendBotMessage>
    {
        public Task Handle(SendBotMessage request, CancellationToken cancellationToken)
        {
            return chatBot.SendMessage(request.Message, request.SourceOnly);
        }
    }
}

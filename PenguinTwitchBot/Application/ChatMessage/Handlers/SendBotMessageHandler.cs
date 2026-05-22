using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Bot.TwitchServices;

namespace PenguinTwitchBot.Application.ChatMessage.Handlers
{
    public class SendBotMessageHandler(ITwitchChatBot chatBot) : Application.Notifications.INotificationHandler<SendBotMessage>
    {
        public Task Handle(SendBotMessage request, CancellationToken cancellationToken)
        {
            return chatBot.SendMessage(request.Message, request.SourceOnly);
        }
    }
}

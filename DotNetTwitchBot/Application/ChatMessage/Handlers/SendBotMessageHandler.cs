using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Handlers
{
    public class SendBotMessageHandler(ITwitchChatBot chatBot) : INotificationHandler<SendBotMessage>
    {
        public Task Handle(SendBotMessage request, CancellationToken cancellationToken)
        {
            return chatBot.SendMessage(request.Message, request.SourceOnly);
        }
    }
}

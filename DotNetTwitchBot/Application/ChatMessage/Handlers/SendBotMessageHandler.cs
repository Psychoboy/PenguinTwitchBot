using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.KickServices;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Handlers
{
    public class SendBotMessageHandler(ITwitchChatBot chatBot, IKickService kickService) : INotificationHandler<SendBotMessage>
    {
        public Task Handle(SendBotMessage request, CancellationToken cancellationToken)
        {
            if (request.Platform == PlatformType.Kick)
            {
                return kickService.SendMessage(request.Message);
            }
            return chatBot.SendMessage(request.Message);
        }
    }
}

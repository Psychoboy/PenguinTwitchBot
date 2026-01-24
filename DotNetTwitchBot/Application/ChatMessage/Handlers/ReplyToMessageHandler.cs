using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.KickServices;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Application.ChatMessage.Handlers
{
    public class ReplyToMessageHandler(ITwitchChatBot chatBot, IKickService kickService) : INotificationHandler<ReplyToMessage>
    {
        public Task Handle(ReplyToMessage request, CancellationToken cancellationToken)
        {
            if(request.Platform == PlatformType.Kick)
            {
                return kickService.ReplyToMessage(request.Name, request.MessageId, request.Message);
            }
            return chatBot.ReplyToMessage(request.Name ,request.MessageId, request.Message);
        }
    }
}

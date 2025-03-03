using DotNetTwitchBot.Application.ChatMessage.Notifications;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public class MarkovChatHistoryNotification(IMarkovChat markovChat) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            markovChat.LearnMessage(notification.EventArgs);
            return Task.CompletedTask;
        }
    }
}

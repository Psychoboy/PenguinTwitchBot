using DotNetTwitchBot.Application.ChatMessage.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public class MarkovChatHistoryNotification(IMarkovChat markovChat) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            markovChat.LearnMessage(notification.EventArgs);
            return Task.CompletedTask;
        }
    }
}

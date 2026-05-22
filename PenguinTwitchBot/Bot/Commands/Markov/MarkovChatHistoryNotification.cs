using PenguinTwitchBot.Application.ChatMessage.Notifications;

namespace PenguinTwitchBot.Bot.Commands.Markov
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

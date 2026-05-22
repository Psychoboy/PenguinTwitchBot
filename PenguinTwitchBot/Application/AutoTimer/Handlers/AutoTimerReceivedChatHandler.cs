using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Misc;

namespace PenguinTwitchBot.Application.AutoTimer.Handlers
{
    public class AutoTimerReceivedChatHandler(AutoTimers autoTimer) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return autoTimer.OnChatMessage();
        }
    }
}

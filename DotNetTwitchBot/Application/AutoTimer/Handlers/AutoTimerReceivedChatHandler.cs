using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Misc;

namespace DotNetTwitchBot.Application.AutoTimer.Handlers
{
    public class AutoTimerReceivedChatHandler(AutoTimers autoTimer) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return autoTimer.OnChatMessage();
        }
    }
}

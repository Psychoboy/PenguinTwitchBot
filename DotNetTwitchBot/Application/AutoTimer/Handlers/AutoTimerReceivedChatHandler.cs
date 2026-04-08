using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.AutoTimer.Handlers
{
    public class AutoTimerReceivedChatHandler(AutoTimers autoTimer) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return autoTimer.OnChatMessage();
        }
    }
}

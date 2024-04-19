using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;
using MediatR;
using Prometheus;

namespace DotNetTwitchBot.Application.Metrics.Handlers
{
    public class IncreaseChatMessageCountHandler(IServiceBackbone serviceBackbone) : INotificationHandler<ReceivedChatMessage>
    {
        private readonly ICollector<ICounter> ChatMessagesCounter = Prometheus.Metrics.WithManagedLifetime(TimeSpan.FromHours(1)).CreateCounter("chat_messages", "Counter of how many chat messages came in.", ["viewer"]).WithExtendLifetimeOnUse();
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            var name = notification.EventArgs.Name;
            if (string.IsNullOrWhiteSpace(name) == false && serviceBackbone.IsKnownBot(name) == false)
            {
                ChatMessagesCounter.WithLabels(notification.EventArgs.Name).Inc();
            }
            return Task.CompletedTask;
        }
    }
}

using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;
using Prometheus;

namespace DotNetTwitchBot.Application.Metrics.Handlers
{
    public class IncreaseNumberOfCommandsHandler : INotificationHandler<RunCommandNotification>, INotificationHandler<StreamStartedNotification>
    {
        private static readonly Gauge NumberOfCommands = Prometheus.Metrics.CreateGauge("number_of_commands", "Number of commands used since last restart", labelNames: ["command", "viewer"]);
        public Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            if (eventArgs != null)
            {
                NumberOfCommands.WithLabels(eventArgs.Command, eventArgs.Name).Inc();
            }
            return Task.CompletedTask;
        }

        public Task Handle(StreamStartedNotification notification, CancellationToken cancellationToken)
        {
            var labels = NumberOfCommands.GetAllLabelValues();
            foreach (var label in labels)
            {
                NumberOfCommands.RemoveLabelled(label);
            }
            return Task.CompletedTask;
        }
    }
}

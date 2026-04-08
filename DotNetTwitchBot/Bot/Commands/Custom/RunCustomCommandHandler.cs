
namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class RunCustomCommandHandler(Commands.Custom.CustomCommand customCommand, ILogger<RunCustomCommandHandler> logger) : Application.Notifications.INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                var eventArgs = notification.EventArgs;
                if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
                await customCommand.RunCommand(eventArgs);
            }
            catch (SkipCooldownException)
            {
                //ignore
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running command: {command}", notification.EventArgs?.Command);
            }
        }
    }
}

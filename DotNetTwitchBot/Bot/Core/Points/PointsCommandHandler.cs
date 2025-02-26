using DotNetTwitchBot.Bot.Commands;
using MediatR;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class PointsCommandHandler(
        IPointsSystem pointsSystem,
        ICommandHandler commandHandler,
        ILogger<PointsCommandHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                var eventArgs = notification.EventArgs;
                if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
                var pointCommand = await pointsSystem.GetPointCommand(eventArgs.Command);
                if (pointCommand == null) return;
                if(pointCommand.Disabled) return;
                if(CommandHandler.CheckToRunBroadcasterOnly(eventArgs, pointCommand) == false) return;
                if(await commandHandler.CheckPermission(pointCommand, eventArgs) == false) return;

                bool isCoolDownExpired = false;
                if (pointCommand.SayCooldown)
                {
                    isCoolDownExpired = await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, pointCommand);
                }
                else
                {
                    isCoolDownExpired = await commandHandler.IsCoolDownExpired(eventArgs.Name, pointCommand.CommandName);
                }
                if(isCoolDownExpired == false) return;

                await pointsSystem.RunCommand(eventArgs, pointCommand);
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

using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Actions
{
    public class ActionCommandHandler(
        IServiceScopeFactory serviceScopeFactory,
        ICommandHandler commandHandler,
        ILogger<ActionCommandHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        SemaphoreSlim cmdLock = new(1, 1);
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                await cmdLock.WaitAsync(cancellationToken);
                if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Command))
                    return;

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
                var actionCommandService = scope.ServiceProvider.GetRequiredService<IActionCommandService>();

                // Get the action command to check its properties
                var actionCommand = await actionCommandService.GetByCommandNameAsync(notification.EventArgs.Command); 

                if (actionCommand == null) return;

                // Check if command is disabled
                if (actionCommand.Disabled) return;

                // Check broadcaster-only restriction
                if (!CommandHandler.CheckIfAllowedInSharedChat(notification.EventArgs, actionCommand))
                {
                    logger.LogWarning("User {User} attempted to run broadcaster-only command {Command}", notification.EventArgs.DisplayName, actionCommand.CommandName);
                    return;
                }

                // Check permissions
                if (!await commandHandler.CheckPermission(actionCommand, notification.EventArgs))
                {
                    logger.LogWarning("User {User} does not have permission to run command {Command}", notification.EventArgs.DisplayName, actionCommand.CommandName);
                    return;
                }

                // Check cooldowns
                if (actionCommand.SayCooldown)
                {
                    if (!await commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(
                        notification.EventArgs.Name,
                        notification.EventArgs.DisplayName,
                        actionCommand.CommandName))
                        return;
                }
                else
                {
                    if (!await commandHandler.IsCoolDownExpired(
                        notification.EventArgs.Name,
                        actionCommand.CommandName))
                        return;
                }

                // Get and execute actions
                var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(
                    Models.Actions.Triggers.TriggerTypes.Command,
                    "!" + notification.EventArgs.Command);

                var dictionary = CommandEventArgsConverter.ToDictionary(notification.EventArgs);

                foreach (var action in actions)
                {
                    await actionService.EnqueueAction(dictionary, action);
                }

                // Set cooldowns after successful execution
                if (actionCommand.GlobalCooldown > 0)
                {
                    await commandHandler.AddGlobalCooldown(actionCommand.CommandName, actionCommand.GlobalCooldown);
                }

                if (actionCommand.UserCooldown > 0)
                {
                    await commandHandler.AddCoolDown(
                        notification.EventArgs.Name,
                        actionCommand.CommandName,
                        actionCommand.UserCooldown);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling action command {Command}", notification.EventArgs?.Command);
            }
            finally
            {
                cmdLock.Release();
            }
        }
    }
}

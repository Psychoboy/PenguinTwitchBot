using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Commands.Actions
{
    public class ActionCommandHandler(
        IServiceScopeFactory serviceScopeFactory,
        ICommandHandler commandHandler,
        ILogger<ActionCommandHandler> logger) : Application.Notifications.INotificationHandler<RunCommandNotification>
    {
        static readonly ConcurrentDictionary<string, SemaphoreSlim> commandLocks = new(StringComparer.OrdinalIgnoreCase);

        static SemaphoreSlim GetLock(string commandName) => commandLocks.GetOrAdd(commandName, _ => new SemaphoreSlim(1, 1));

        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Command))
                return;

            var cmdLock = GetLock(notification.EventArgs.Command);
            await cmdLock.WaitAsync(cancellationToken);
            try
            {
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
                     TriggerTypes.Command,
                    "!" + notification.EventArgs.Command);

                var dictionary = CommandEventArgsConverter.ToDictionary(notification.EventArgs);
                dictionary[ActionExecutionVariableKeys.CooldownCommandName] = actionCommand.CommandName;
                dictionary[ActionExecutionVariableKeys.CooldownUserName] = notification.EventArgs.Name;
                dictionary[ActionExecutionVariableKeys.TriggerDisplayName] = notification.EventArgs.DisplayName;

                // Set cooldowns before enqueue to close race windows between rapid invocations.
                if (actionCommand.GlobalCooldown > 0)
                {
                    var globalCooldown = CooldownHelper.CalculateCooldown(actionCommand.GlobalCooldown, actionCommand.GlobalCooldownMax);
                    await commandHandler.AddGlobalCooldown(actionCommand.CommandName, globalCooldown);
                }

                if (actionCommand.UserCooldown > 0)
                {
                    var userCooldown = CooldownHelper.CalculateCooldown(actionCommand.UserCooldown, actionCommand.UserCooldownMax);
                    await commandHandler.AddCoolDown(
                        notification.EventArgs.Name,
                        actionCommand.CommandName,
                        userCooldown);
                }

                foreach (var action in actions)
                {
                    await actionService.EnqueueAction(dictionary, action);
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

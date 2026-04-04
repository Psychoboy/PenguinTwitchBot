using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Utilities;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Actions
{
    public class ActionCommandHandler(
        IServiceScopeFactory serviceScopeFactory,
        ICommandHandler commandHandler,
        ILogger<ActionCommandHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Command))
                return;

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
            var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
            var actionCommandService = scope.ServiceProvider.GetRequiredService<IActionCommandService>();

            // Get the action command to check its properties
            var actionCommands = await actionCommandService.GetAllAsync();
            var actionCommand = actionCommands.FirstOrDefault(c => 
                c.CommandName.Equals(notification.EventArgs.Command, StringComparison.OrdinalIgnoreCase));

            if (actionCommand == null) return;

            // Check if command is disabled
            if (actionCommand.Disabled) return;

            // Check broadcaster-only restriction
            if (!CommandHandler.CheckToRunBroadcasterOnly(notification.EventArgs, actionCommand))
                return;

            // Check permissions
            if (!await commandHandler.CheckPermission(actionCommand, notification.EventArgs))
                return;

            // Check cooldowns
            if (actionCommand.SayCooldown)
            {
                if (!await commandHandler.IsCoolDownExpiredWithMessage(
                    notification.EventArgs.Name, 
                    notification.EventArgs.DisplayName, 
                    actionCommand))
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
    }
}

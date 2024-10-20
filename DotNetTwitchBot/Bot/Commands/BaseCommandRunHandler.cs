using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands
{
    public class BaseCommandRunHandler(ICommandHandler commandHandler, ILogger<BaseCommandRunHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            try
            {
                if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));

                if (eventArgs.SkipLock == false)
                {
                    if (await _semaphoreSlim.WaitAsync(500, cancellationToken) == false)
                    {
                        logger.LogWarning("BaseCommand Lock expired while waiting...");
                    }
                }
                var commandService = commandHandler.GetCommand(eventArgs.Command);
                if (commandService != null && commandService.CommandProperties.Disabled == false && CommandHandler.CheckToRunBroadcasterOnly(eventArgs, commandService.CommandProperties))
                {
                    if (await commandHandler.CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        if (commandService.CommandProperties.SayCooldown)
                        {
                            if (await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, commandService.CommandProperties) == false) return;
                        }
                        else
                        {
                            if (commandHandler.IsCoolDownExpired(eventArgs.Name, commandService.CommandProperties.CommandName) == false) return;
                        }
                        //This will throw a SkipCooldownException if the command fails to by pass setting cooldown
                        await commandService.CommandService.OnCommand(this, eventArgs);
                    }
                    else
                    {
                        return;
                    }

                    if (commandService.CommandProperties.GlobalCooldown > 0)
                    {
                        commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, commandService.CommandProperties.GlobalCooldown);
                    }

                    if (commandService.CommandProperties.UserCooldown > 0)
                    {
                        commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, commandService.CommandProperties.UserCooldown);
                    }
                }
            }
            catch (SkipCooldownException)
            {
                //ignore
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running command: {command}", notification.EventArgs?.Command);
            }
            finally
            {
                if (eventArgs?.SkipLock == false)
                    _semaphoreSlim.Release();
            }
        }
    }
}

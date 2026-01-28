using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands
{
    public class BaseCommandRunHandler(ICommandHandler commandHandler, ILogger<BaseCommandRunHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        static readonly ConcurrentDictionary<string, SemaphoreSlim> CommandLock = new ConcurrentDictionary<string, SemaphoreSlim>();
        private string lastCommand = string.Empty;
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            SemaphoreSlim? lockInstance = null;
            try
            {
                if (eventArgs == null) throw new ArgumentNullException("eventArgs");

                if (eventArgs.SkipLock == false)
                {
                    lockInstance = CommandLock.GetOrAdd(eventArgs.Command, x => new SemaphoreSlim(1));
                    if (await lockInstance.WaitAsync(10000, cancellationToken) == false)
                    {
                        logger.LogWarning("BaseCommand Lock expired while waiting... Last Locked Command: {lastCommand}", lastCommand);
                    }
                    lastCommand = eventArgs.Command;
                }
                var commandService = commandHandler.GetCommand(eventArgs.Command);
                if (commandService != null && commandService.CommandProperties.Disabled == false && CommandHandler.CheckToRunBroadcasterOnly(eventArgs, commandService.CommandProperties))
                {
                    if(!commandService.CommandProperties.Platforms.Contains(eventArgs.Platform))
                    {
                        return;
                    }

                    if (await commandHandler.CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        if (commandService.CommandProperties.SayCooldown)
                        {
                            if (await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.Platform, eventArgs.DisplayName, commandService.CommandProperties) == false) return;
                        }
                        else
                        {
                            if (await commandHandler.IsCoolDownExpired(eventArgs.Name, eventArgs.Platform, commandService.CommandProperties.CommandName) == false) return;
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
                        await commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, commandService.CommandProperties.GlobalCooldown);
                    }

                    if (commandService.CommandProperties.UserCooldown > 0)
                    {
                        await commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, commandService.CommandProperties.UserCooldown);
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
                    lockInstance?.Release();
            }
        }
    }
}

using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.Commands;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    /// <summary>
    /// Example handler for runtime-only SubAction.
    /// This handler will work normally but the SubAction type will never be persisted.
    /// </summary>
    public class RuntimeDefaultCommandHandler(ICommandHandler commandHandler, ILogger<RuntimeDefaultCommandHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RuntimeDefaultCommand;
        static readonly ConcurrentDictionary<string, SemaphoreSlim> CommandLock = new ConcurrentDictionary<string, SemaphoreSlim>();
        private string lastCommand = string.Empty;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not RuntimeDefaultCommandType runtimeAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid SubActionType passed to RuntimeDefaultCommandHandler: {SubActionType}", subAction.GetType().Name);
            }

            var eventArgs = Utilities.CommandEventArgsConverter.FromDictionaryOrNull(variables) ?? throw new SubActionHandlerException(subAction, "Failed to convert variables to CommandEventArgs for RuntimeDefaultCommandHandler.");
            SemaphoreSlim? lockInstance = null;
            try
            {
                

                if (eventArgs.SkipLock == false)
                {
                    lockInstance = CommandLock.GetOrAdd(eventArgs.Command, x => new SemaphoreSlim(1));
                    if (await lockInstance.WaitAsync(10000) == false)
                    {
                        logger.LogWarning("BaseCommand Lock expired while waiting... Last Locked Command: {lastCommand}", lastCommand);
                    }
                    lastCommand = eventArgs.Command;
                }
                var commandService = commandHandler.GetCommand(eventArgs.Command);
                if (commandService != null && commandService.CommandProperties.Disabled == false && CommandHandler.CheckIfAllowedInSharedChat(eventArgs, commandService.CommandProperties))
                {
                    if (await commandHandler.CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        if (commandService.CommandProperties.SayCooldown)
                        {
                            if (await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, commandService.CommandProperties) == false) return;
                        }
                        else
                        {
                            if (await commandHandler.IsCoolDownExpired(eventArgs.Name, commandService.CommandProperties.CommandName) == false) return;
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
                        var globalCooldown = Bot.Commands.CooldownHelper.CalculateCooldown(commandService.CommandProperties.GlobalCooldown, commandService.CommandProperties.GlobalCooldownMax);
                        await commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, globalCooldown);
                    }

                    if (commandService.CommandProperties.UserCooldown > 0)
                    {
                        var userCooldown = Bot.Commands.CooldownHelper.CalculateCooldown(commandService.CommandProperties.UserCooldown, commandService.CommandProperties.UserCooldownMax);
                        await commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, userCooldown);
                    }
                }
            }
            catch (SkipCooldownException)
            {
                //ignore
            }
            //All other exceptions bubble up and are handled by the calling code, which will log them and handle any necessary cleanup.
            //We only catch SkipCooldownException here to prevent cooldowns from being set when a command fails to bypass cooldown settings.
            finally
            {
                if (eventArgs?.SkipLock == false)
                    lockInstance?.Release();
            }
        }
    }
}


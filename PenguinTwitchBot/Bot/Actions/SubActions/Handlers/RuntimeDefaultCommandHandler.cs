using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RuntimeDefaultCommandHandler(ICommandHandler commandHandler, ILogger<RuntimeDefaultCommandHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RuntimeDefaultCommand;
        static readonly ConcurrentDictionary<string, SemaphoreSlim> CommandLock = new ConcurrentDictionary<string, SemaphoreSlim>();
        private string lastCommand = string.Empty;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context, int subActionIndex)
        {
            if (subAction is not RuntimeDefaultCommandType)
            {
                throw new SubActionHandlerException(subAction, "Invalid SubActionType passed to RuntimeDefaultCommandHandler: {SubActionType}", subAction.GetType().Name);
            }

            var eventArgs = Utilities.CommandEventArgsConverter.FromDictionaryOrNull(variables) ?? throw new SubActionHandlerException(subAction, "Failed to convert variables to CommandEventArgs for RuntimeDefaultCommandHandler.");
            SemaphoreSlim? lockInstance = null;
            try
            {
                lockInstance = await AcquireCommandLockAsync(eventArgs);
                var commandService = commandHandler.GetCommand(eventArgs.Command);
                if (commandService == null) return;

                if (!await TryRunCommandAsync(eventArgs, commandService)) return;
                await ApplyCooldownsAsync(eventArgs, commandService);
            }
            catch (SkipCooldownException)
            {
                //ignore
            }
            finally
            {
                if (eventArgs?.SkipLock == false)
                    lockInstance?.Release();
            }
        }

        private async Task<SemaphoreSlim?> AcquireCommandLockAsync(CommandEventArgs eventArgs)
        {
            if (eventArgs.SkipLock) return null;

            var lockInstance = CommandLock.GetOrAdd(eventArgs.Command, _ => new SemaphoreSlim(1));
            if (await lockInstance.WaitAsync(10000) == false)
            {
                logger.LogWarning("BaseCommand Lock expired while waiting... Last Locked Command: {lastCommand}", lastCommand);
            }
            lastCommand = eventArgs.Command;
            return lockInstance;
        }

        private async Task<bool> TryRunCommandAsync(CommandEventArgs eventArgs, Command commandService)
        {
            if (commandService.CommandProperties.Disabled == false && CommandHandler.CheckIfAllowedInSharedChat(eventArgs, commandService.CommandProperties))
            {
                if (await commandHandler.CheckPermission(commandService.CommandProperties, eventArgs))
                {
                    if (commandService.CommandProperties.SayCooldown)
                    {
                        if (await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, commandService.CommandProperties) == false) return false;
                    }
                    else
                    {
                        if (await commandHandler.IsCoolDownExpired(eventArgs.Name, commandService.CommandProperties.CommandName) == false) return false;
                    }
                    await commandService.CommandService.OnCommand(this, eventArgs);
                    return true;
                }
            }
            return false;
        }

        private async Task ApplyCooldownsAsync(CommandEventArgs eventArgs, Command commandService)
        {
            if (commandService.CommandProperties.GlobalCooldown > 0)
            {
                var globalCooldown = CooldownHelper.CalculateCooldown(commandService.CommandProperties.GlobalCooldown, commandService.CommandProperties.GlobalCooldownMax);
                await commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, globalCooldown);
            }

            if (commandService.CommandProperties.UserCooldown > 0)
            {
                var userCooldown = CooldownHelper.CalculateCooldown(commandService.CommandProperties.UserCooldown, commandService.CommandProperties.UserCooldownMax);
                await commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, userCooldown);
            }
        }
    }
}


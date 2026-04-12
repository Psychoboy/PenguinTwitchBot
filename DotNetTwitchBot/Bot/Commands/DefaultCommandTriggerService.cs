using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.Triggers.Configurations;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface IDefaultCommandTriggerService
    {
        Task TriggerDefaultCommandEventAsync(string defaultCommandName, string eventType, CommandEventArgs eventArgs, Dictionary<string, string>? additionalVariables = null);
    }

    public class DefaultCommandTriggerService : IDefaultCommandTriggerService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DefaultCommandTriggerService> _logger;

        public DefaultCommandTriggerService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DefaultCommandTriggerService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task TriggerDefaultCommandEventAsync(
            string defaultCommandName,
            string eventType,
            CommandEventArgs eventArgs,
            Dictionary<string, string>? additionalVariables = null)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                // Get the default command by name
                var defaultCommand = await unitOfWork.DefaultCommands
                    .Find(x => x.CommandName.Equals(defaultCommandName))
                    .FirstOrDefaultAsync();

                if (defaultCommand == null)
                {
                    _logger.LogWarning("Default command not found: {CommandName}", defaultCommandName);
                    return;
                }

                if (!defaultCommand.Id.HasValue)
                {
                    _logger.LogWarning("Default command has no ID: {CommandName}", defaultCommandName);
                    return;
                }

                // Get all triggers for this default command
                var triggers = await unitOfWork.Triggers.GetByDefaultCommandIdAsync(defaultCommand.Id.Value);

                if (triggers.Count == 0)
                {
                    _logger.LogDebug("No triggers found for default command {CommandName} event {EventType}", 
                        defaultCommandName, eventType);
                    return;
                }

                // Filter triggers by event type from their configuration
                var matchingTriggers = new List<TriggerType>();
                foreach (var trigger in triggers)
                {
                    if (!trigger.Enabled)
                        continue;

                    try
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(
                            trigger.Configuration);
                        
                        if (config != null && config.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingTriggers.Add(trigger);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing trigger configuration for trigger {TriggerId}", trigger.Id);
                    }
                }

                if (matchingTriggers.Count == 0)
                {
                    _logger.LogDebug("No enabled triggers found for default command {CommandName} event {EventType}", 
                        defaultCommandName, eventType);
                    return;
                }

                // Build the variables dictionary
                var variables = CommandEventArgsConverter.ToDictionary(eventArgs);
                
                // Add any additional variables
                if (additionalVariables != null)
                {
                    foreach (var kvp in additionalVariables)
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
                }

                // Execute all matching triggers
                foreach (var trigger in matchingTriggers)
                {
                    if (trigger.Action != null)
                    {
                        _logger.LogInformation(
                            "Triggering action {ActionName} for default command {CommandName} event {EventType}",
                            trigger.Action.Name, defaultCommandName, eventType);

                        await actionService.EnqueueAction(variables, trigger.Action);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error triggering default command event for {CommandName} event {EventType}", 
                    defaultCommandName, eventType);
            }
        }
    }
}

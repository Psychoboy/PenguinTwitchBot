using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.Models.Timers;
using DotNetTwitchBot.Repository;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Validation
{
    public class ValidationService : IValidationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ValidationService> _logger;
        private ValidationResult? _lastValidationResult;
        private readonly object _lock = new();

        public ValidationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ValidationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateTriggersAndSubActionsAsync()
        {
            _logger.LogInformation("Starting validation of triggers and subactions");
            var result = new ValidationResult();

            var triggerResult = await ValidateTriggersAsync();
            var subActionResult = await ValidateSubActionsAsync();

            result.Issues.AddRange(triggerResult.Issues);
            result.Issues.AddRange(subActionResult.Issues);

            _logger.LogInformation(
                "Validation completed. Found {ErrorCount} errors and {WarningCount} warnings",
                result.ErrorCount,
                result.WarningCount);

            // Cache the result
            lock (_lock)
            {
                _lastValidationResult = result;
            }

            return result;
        }

        public ValidationResult? GetLastValidationResult()
        {
            lock (_lock)
            {
                return _lastValidationResult;
            }
        }

        public async Task<ValidationResult> ValidateTriggersAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get all triggers
            var allTriggers = await db.Triggers.GetAllAsync();
            _logger.LogDebug("Validating {TriggerCount} triggers", allTriggers.Count);

            // Get all timer groups and commands for validation - with full entities for name validation
            var timerGroups = await db.TimerGroups.GetAsync();
            var timerGroupDict = timerGroups
                .Where(t => t.Id.HasValue)
                .ToDictionary(t => t.Id!.Value, t => t);

            var commands = await db.ActionCommands.GetAsync();
            var commandDict = commands
                .Where(c => c.Id.HasValue)
                .ToDictionary(c => c.Id!.Value, c => c);

            foreach (var trigger in allTriggers)
            {
                // Validate trigger name
                ValidateTriggerName(trigger, result);

                // Validate Timer triggers
                if (trigger.Type == TriggerTypes.Timer)
                {
                    ValidateTimerTrigger(trigger, timerGroupDict, result);
                }

                // Validate Command triggers
                if (trigger.Type == TriggerTypes.Command)
                {
                    ValidateCommandTrigger(trigger, commandDict, result);
                }
            }

            _logger.LogDebug("Trigger validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        public async Task<ValidationResult> ValidateSubActionsAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pointsSystem = scope.ServiceProvider.GetRequiredService<IPointsSystem>();

            // Get all actions with their subactions
            var allActions = await db.Actions.GetAllWithDetailsAsync();
            _logger.LogDebug("Validating subactions across {ActionCount} actions", allActions.Count);

            // Get all commands and actions for validation
            var commands = await db.ActionCommands.GetAsync();
            var commandIds = new HashSet<int>(commands.Where(c => c.Id.HasValue).Select(c => c.Id!.Value));
            var commandNames = new HashSet<string>(commands.Select(c => c.CommandName), StringComparer.OrdinalIgnoreCase);

            var actionIds = new HashSet<int>(allActions.Where(a => a.Id.HasValue).Select(a => a.Id!.Value));

            // Get all default commands for ExecuteDefaultCommand validation
            var defaultCommands = await db.DefaultCommands.GetAllAsync();
            var defaultCommandNames = new HashSet<string>(defaultCommands.Select(dc => dc.CommandName), StringComparer.OrdinalIgnoreCase);

            // Get all point types for CheckPoints validation
            var pointTypes = await pointsSystem.GetPointTypes();
            var pointTypeNames = new HashSet<string>(pointTypes.Select(pt => pt.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var action in allActions)
            {
                ValidateSubActionList(action.SubActions, commandIds, commandNames, actionIds, defaultCommandNames, pointTypeNames, result, action.Id, action.Name);
            }

            _logger.LogDebug("SubAction validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        private void ValidateSubActionList(
            List<SubActionType> subActions, 
            HashSet<int> commandIds,
            HashSet<string> commandNames,
            HashSet<int> actionIds,
            HashSet<string> defaultCommandNames,
            HashSet<string> pointTypeNames,
            ValidationResult result,
            int? parentActionId,
            string parentActionName)
        {
            foreach (var subAction in subActions)
            {
                // Validate ToggleCommandDisabled subactions
                if (subAction is ToggleCommandDisabledType toggleCommand)
                {
                    if (!string.IsNullOrWhiteSpace(toggleCommand.CommandName) && !commandNames.Contains(toggleCommand.CommandName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionCommandNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Toggle Command Disabled",
                            Message = $"SubAction 'Toggle Command Disabled' references non-existent Command: {toggleCommand.CommandName}",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ExecuteAction subactions
                if (subAction is ExecuteActionType executeAction)
                {
                    if (executeAction.ActionId.HasValue && executeAction.ActionId.Value > 0 && !actionIds.Contains(executeAction.ActionId.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionActionNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Execute Action",
                            Message = $"SubAction 'Execute Action' references non-existent Action ID: {executeAction.ActionId} (Name: {executeAction.ActionName})",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ExecuteDefaultCommand subactions
                if (subAction is ExecuteDefaultCommandType executeDefaultCommand)
                {
                    if (!string.IsNullOrWhiteSpace(executeDefaultCommand.CommandName) && 
                        !defaultCommandNames.Contains(executeDefaultCommand.CommandName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionDefaultCommandNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Execute Default Command",
                            Message = $"SubAction 'Execute Default Command' references non-existent Default Command: '{executeDefaultCommand.CommandName}'",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(executeDefaultCommand.CommandName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionDefaultCommandNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Execute Default Command",
                            Message = "SubAction 'Execute Default Command' has missing or empty CommandName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate CheckPoints subactions
                if (subAction is CheckPointsType checkPoints)
                {
                    if (!string.IsNullOrWhiteSpace(checkPoints.PointTypeName) && 
                        !pointTypeNames.Contains(checkPoints.PointTypeName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionPointTypeNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Check Points",
                            Message = $"SubAction 'Check Points' references non-existent Point Type: '{checkPoints.PointTypeName}'",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(checkPoints.PointTypeName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionPointTypeNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Check Points",
                            Message = "SubAction 'Check Points' has missing or empty PointTypeName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate nested subactions in LogicIfElse
                if (subAction is LogicIfElseType logicIfElse)
                {
                    // Recursively validate TrueSubActions
                    if (logicIfElse.TrueSubActions?.Count > 0)
                    {
                        ValidateSubActionList(logicIfElse.TrueSubActions, commandIds, commandNames, actionIds, defaultCommandNames, pointTypeNames, result, parentActionId, parentActionName);
                    }

                    // Recursively validate FalseSubActions
                    if (logicIfElse.FalseSubActions?.Count > 0)
                    {
                        ValidateSubActionList(logicIfElse.FalseSubActions, commandIds, commandNames, actionIds, defaultCommandNames, pointTypeNames, result, parentActionId, parentActionName);
                    }
                }
            }
        }

        public async Task MarkEntityWithIssueAsync(string entityType, int entityId, bool hasIssue)
        {
            // This could be implemented to add a flag to the database entities
            // For now, we'll just log it
            _logger.LogInformation("Entity {EntityType} {EntityId} marked with issue flag: {HasIssue}", 
                entityType, entityId, hasIssue);
            await Task.CompletedTask;
        }

        public async Task ClearAllValidationMarksAsync()
        {
            _logger.LogInformation("Clearing all validation marks");
            await Task.CompletedTask;
        }

        private void ValidateTriggerName(TriggerType trigger, ValidationResult result)
        {
            // Check if name is null/empty/whitespace
            if (string.IsNullOrWhiteSpace(trigger.Name))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerInvalidName,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name ?? "(unnamed)",
                    Message = $"Trigger (ID: {trigger.Id}) has an invalid or missing Name",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }
        }

        private void ValidateTimerTrigger(TriggerType trigger, Dictionary<int, TimerGroup> timerGroupDict, ValidationResult result)
        {
            var configData = ParseTimerTriggerConfiguration(trigger.Configuration);
            var configTimerGroupId = configData?.TimerGroupId;
            var configTimerGroupName = configData?.TimerGroupName;
            var hasReferenceColumn = trigger.TimerGroupId.HasValue;
            var hasConfigValue = configTimerGroupId.HasValue;

            // Validate Timer trigger name format: should be "TimerGroup_{id}"
            var timerGroupIdToCheck = trigger.TimerGroupId ?? configTimerGroupId;
            if (timerGroupIdToCheck.HasValue && !string.IsNullOrWhiteSpace(trigger.Name))
            {
                var expectedName = $"TimerGroup_{timerGroupIdToCheck.Value}";
                if (!trigger.Name.Equals(expectedName, StringComparison.Ordinal))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerInvalidName,
                        Severity = ValidationSeverity.Warning,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"Timer trigger '{trigger.Name}' has incorrect name format. Expected: '{expectedName}'",
                        RelatedActionId = trigger.ActionId,
                        RelatedActionName = trigger.Action?.Name
                    });
                }
            }

            // Check if both are missing
            if (!hasReferenceColumn && !hasConfigValue)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Timer trigger '{trigger.Name}' is missing both TimerGroupId reference column and Configuration data",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
                return;
            }

            // Check if Configuration is invalid/missing but reference column exists
            if (hasReferenceColumn && !hasConfigValue && !string.IsNullOrWhiteSpace(trigger.Configuration))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerInvalidConfiguration,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Timer trigger '{trigger.Name}' has invalid or malformed Configuration JSON (missing TimerGroupId)",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if Configuration is completely missing
            if (hasReferenceColumn && string.IsNullOrWhiteSpace(trigger.Configuration))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Timer trigger '{trigger.Name}' is missing Configuration JSON data",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if reference column is missing but Configuration has a value
            if (!hasReferenceColumn && hasConfigValue)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerConfigurationMismatch,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Timer trigger '{trigger.Name}' has TimerGroupId in Configuration ({configTimerGroupId}) but missing reference column",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if both exist but don't match
            if (hasReferenceColumn && hasConfigValue && trigger.TimerGroupId != configTimerGroupId)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerConfigurationIdMismatch,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Timer trigger '{trigger.Name}' has mismatched TimerGroupId: reference column={trigger.TimerGroupId}, Configuration={configTimerGroupId}",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if the referenced TimerGroup exists
            if (timerGroupIdToCheck.HasValue)
            {
                if (!timerGroupDict.TryGetValue(timerGroupIdToCheck.Value, out var timerGroup))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerTimerGroupNotFound,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"Timer trigger '{trigger.Name}' references non-existent TimerGroup ID: {timerGroupIdToCheck}",
                        RelatedActionId = trigger.ActionId,
                        RelatedActionName = trigger.Action?.Name
                    });
                }
                else
                {
                    // Validate TimerGroupName if it exists in configuration
                    if (!string.IsNullOrWhiteSpace(configTimerGroupName))
                    {
                        if (!configTimerGroupName.Equals(timerGroup.Name, StringComparison.Ordinal))
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                IssueType = ValidationIssueType.TriggerConfigurationNameMismatch,
                                Severity = ValidationSeverity.Warning,
                                EntityType = "Trigger",
                                EntityId = trigger.Id,
                                EntityName = trigger.Name,
                                Message = $"Timer trigger '{trigger.Name}' has incorrect TimerGroupName in Configuration: expected '{timerGroup.Name}', found '{configTimerGroupName}'",
                                RelatedActionId = trigger.ActionId,
                                RelatedActionName = trigger.Action?.Name
                            });
                        }
                    }
                }
            }
        }

        private void ValidateCommandTrigger(TriggerType trigger, Dictionary<int, ActionCommand> commandDict, ValidationResult result)
        {
            var configData = ParseCommandTriggerConfiguration(trigger.Configuration);
            var configCommandId = configData?.CommandId;
            var configCommandName = configData?.CommandName;
            var hasReferenceColumn = trigger.CommandId.HasValue;
            var hasConfigValue = configCommandId.HasValue;

            // Validate Command trigger name format: should be "!{CommandName}"
            var commandIdToCheck = trigger.CommandId ?? configCommandId;
            if (commandIdToCheck.HasValue && !string.IsNullOrWhiteSpace(trigger.Name))
            {
                if (commandDict.TryGetValue(commandIdToCheck.Value, out var command))
                {
                    var expectedName = $"!{command.CommandName}";
                    if (!trigger.Name.Equals(expectedName, StringComparison.Ordinal))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.TriggerInvalidName,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "Trigger",
                            EntityId = trigger.Id,
                            EntityName = trigger.Name,
                            Message = $"Command trigger '{trigger.Name}' has incorrect name format. Expected: '{expectedName}'",
                            RelatedActionId = trigger.ActionId,
                            RelatedActionName = trigger.Action?.Name
                        });
                    }
                }
            }

            // Check if both are missing
            if (!hasReferenceColumn && !hasConfigValue)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Command trigger '{trigger.Name}' is missing both CommandId reference column and Configuration data",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
                return;
            }

            // Check if Configuration is invalid/missing but reference column exists
            if (hasReferenceColumn && !hasConfigValue && !string.IsNullOrWhiteSpace(trigger.Configuration))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerInvalidConfiguration,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Command trigger '{trigger.Name}' has invalid or malformed Configuration JSON (missing CommandId)",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if Configuration is completely missing
            if (hasReferenceColumn && string.IsNullOrWhiteSpace(trigger.Configuration))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Command trigger '{trigger.Name}' is missing Configuration JSON data",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if reference column is missing but Configuration has a value
            if (!hasReferenceColumn && hasConfigValue)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerConfigurationMismatch,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Command trigger '{trigger.Name}' has CommandId in Configuration ({configCommandId}) but missing reference column",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if both exist but don't match
            if (hasReferenceColumn && hasConfigValue && trigger.CommandId != configCommandId)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerConfigurationIdMismatch,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"Command trigger '{trigger.Name}' has mismatched CommandId: reference column={trigger.CommandId}, Configuration={configCommandId}",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }

            // Check if the referenced Command exists
            if (commandIdToCheck.HasValue)
            {
                if (!commandDict.TryGetValue(commandIdToCheck.Value, out var command))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerCommandNotFound,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"Command trigger '{trigger.Name}' references non-existent Command ID: {commandIdToCheck}",
                        RelatedActionId = trigger.ActionId,
                        RelatedActionName = trigger.Action?.Name
                    });
                }
                else
                {
                    // Validate CommandName if it exists in configuration
                    if (!string.IsNullOrWhiteSpace(configCommandName))
                    {
                        if (!configCommandName.Equals(command.CommandName, StringComparison.Ordinal))
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                IssueType = ValidationIssueType.TriggerConfigurationNameMismatch,
                                Severity = ValidationSeverity.Warning,
                                EntityType = "Trigger",
                                EntityId = trigger.Id,
                                EntityName = trigger.Name,
                                Message = $"Command trigger '{trigger.Name}' has incorrect CommandName in Configuration: expected '{command.CommandName}', found '{configCommandName}'",
                                RelatedActionId = trigger.ActionId,
                                RelatedActionName = trigger.Action?.Name
                            });
                        }
                    }
                }
            }
        }

        private TimerTriggerConfig? ParseTimerTriggerConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration);
                if (config == null) return null;

                var result = new TimerTriggerConfig();

                if (config.TryGetValue("TimerGroupId", out var timerGroupIdElement))
                {
                    result.TimerGroupId = timerGroupIdElement.GetInt32();
                }

                if (config.TryGetValue("TimerGroupName", out var timerGroupNameElement))
                {
                    result.TimerGroupName = timerGroupNameElement.GetString();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse Timer trigger configuration");
                return null;
            }
        }

        private CommandTriggerConfig? ParseCommandTriggerConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration);
                if (config == null) return null;

                var result = new CommandTriggerConfig();

                if (config.TryGetValue("CommandId", out var commandIdElement))
                {
                    result.CommandId = commandIdElement.GetInt32();
                }

                if (config.TryGetValue("CommandName", out var commandNameElement))
                {
                    result.CommandName = commandNameElement.GetString();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse Command trigger configuration");
                return null;
            }
        }
    }

    internal class TimerTriggerConfig
    {
        public int? TimerGroupId { get; set; }
        public string? TimerGroupName { get; set; }
    }

    internal class CommandTriggerConfig
    {
        public int? CommandId { get; set; }
        public string? CommandName { get; set; }
    }
}

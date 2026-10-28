using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Validation
{
    public partial class ValidationService : IValidationService
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

            var actionResult = await ValidateActionsAsync();
            var triggerResult = await ValidateTriggersAsync();
            var subActionResult = await ValidateSubActionsAsync();
            var commandResult = await ValidateCommandsAsync();
            var keywordResult = await ValidateKeywordsAsync();
            var configurationResult = await ValidateConfigurationAsync();

            result.Issues.AddRange(actionResult.Issues);
            result.Issues.AddRange(triggerResult.Issues);
            result.Issues.AddRange(subActionResult.Issues);
            result.Issues.AddRange(commandResult.Issues);
            result.Issues.AddRange(keywordResult.Issues);
            result.Issues.AddRange(configurationResult.Issues);

            foreach (var partial in new[] { actionResult, triggerResult, subActionResult, commandResult, keywordResult, configurationResult })
            {
                foreach (var (name, count) in partial.Stats)
                {
                    result.Stats[name] = count;
                }
            }

            var ignoredKeys = await GetIgnoredIssueKeysAsync();
            foreach (var issue in result.Issues.Where(i => ignoredKeys.Contains(i.Key)))
            {
                issue.Ignored = true;
            }

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

        public async Task<ValidationResult> ValidateActionsAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var allActions = await db.Actions.GetAllWithDetailsAsync();
            _logger.LogDebug("Validating {ActionCount} actions", allActions.Count);
            result.Stats["Actions"] = allActions.Count;

            // Track action names to check for duplicates
            var actionNameCounts = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in allActions)
            {
                // Validate action name
                if (string.IsNullOrWhiteSpace(action.Name))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.ActionInvalidName,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Action",
                        EntityId = action.Id,
                        EntityName = action.Name ?? "(unnamed)",
                        Message = $"Action (ID: {action.Id}) has an invalid or missing Name"
                    });
                }
                else
                {
                    // Track for duplicate checking
                    if (!actionNameCounts.ContainsKey(action.Name))
                        actionNameCounts[action.Name] = new List<Guid>();
                    actionNameCounts[action.Name].Add(action.Id);
                }

                // Validate queue name is not empty
                if (string.IsNullOrWhiteSpace(action.QueueName))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.ActionInvalidQueueName,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Action",
                        EntityId = action.Id,
                        EntityName = action.Name ?? "(unknown)",
                        Message = $"Action '{action.Name}' has an invalid or missing QueueName"
                    });
                }

                // Actions without triggers are valid: they can run via the API, ExecuteAction or the test button.

                // Warn about actions with no subactions
                if (action.SubActions == null || action.SubActions.Count == 0)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.ActionNoSubActions,
                        Severity = ValidationSeverity.Warning,
                        EntityType = "Action",
                        EntityId = action.Id,
                        EntityName = action.Name ?? "(unknown)",
                        Message = $"Action '{action.Name}' has no subactions defined"
                    });
                }

                // Validate circular dependencies in ExecuteAction subactions
                ValidateCircularDependencies(action, allActions, result);
            }

            // Report duplicate action names
            foreach (var kvp in actionNameCounts.Where(x => x.Value.Count > 1))
            {
                var duplicateIds = string.Join(", ", kvp.Value.Select(id => id.ToString()));
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.ActionDuplicateName,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Action",
                    EntityId = null,
                    EntityName = kvp.Key,
                    Message = $"Multiple actions found with name '{kvp.Key}' (IDs: {duplicateIds})"
                });
            }

            _logger.LogDebug("Action validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        private void ValidateCircularDependencies(ActionType action, List<ActionType> allActions, ValidationResult result)
        {
            if (action.SubActions == null || action.Id == Guid.Empty)
                return;

            var visited = new HashSet<Guid>();
            var recursionStack = new HashSet<Guid>();

            if (HasCircularDependency(action.Id, allActions, visited, recursionStack, out var cyclePath))
            {
                // Parse the action IDs from the cycle path (format: "1 -> 2 -> 1")
                // and check if any action in the cycle uses the counter-decrement pattern:
                // SetVariable (e.g. $math($var$-1)$) + LogicIfElse to guard the recursive
                // ExecuteAction call. Such cycles will naturally exit and are not infinite loops.
                var cycleActionIds = cyclePath
                    .Split(" -> ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToHashSet();

                var cycleActions = allActions
                    .Where(a => a.Id != Guid.Empty && cycleActionIds.Contains(a.Id));

                if (cycleActions.Any(HasCounterDecrementPattern))
                    return;

                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.ActionCircularDependency,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Action",
                    EntityId = action.Id,
                    EntityName = action.Name,
                    Message = $"Action '{action.Name}' has a circular dependency: {cyclePath}"
                });
            }
        }

        /// <summary>
        /// Returns true when an action contains both a SetVariable subaction and a LogicIfElse
        /// subaction anywhere in its subaction tree. This combination indicates the action uses
        /// a counter-decrement pattern (e.g. <c>$math($var$-1)$</c>) with a conditional exit,
        /// which is intentional bounded recursion rather than an infinite loop.
        /// </summary>
        private static bool HasCounterDecrementPattern(ActionType action)
        {
            var subActions = action.SubActions;
            if (subActions == null || subActions.Count == 0)
                return false;

            return ContainsSubActionOfType<SetVariableType>(subActions) &&
                   ContainsSubActionOfType<LogicIfElseType>(subActions);
        }

        private static bool ContainsSubActionOfType<T>(List<SubActionType> subActions) where T : SubActionType
        {
            foreach (var subAction in subActions)
            {
                if (subAction is T)
                    return true;

                if (subAction is LogicIfElseType logicIfElse)
                {
                    if ((logicIfElse.TrueSubActions != null && ContainsSubActionOfType<T>(logicIfElse.TrueSubActions)) ||
                        (logicIfElse.FalseSubActions != null && ContainsSubActionOfType<T>(logicIfElse.FalseSubActions)))
                        return true;
                }
            }
            return false;
        }

        private bool HasCircularDependency(
            Guid actionId, 
            List<ActionType> allActions, 
            HashSet<Guid> visited, 
            HashSet<Guid> recursionStack,
            out string cyclePath)
        {
            cyclePath = string.Empty;

            if (recursionStack.Contains(actionId))
            {
                cyclePath = actionId.ToString();
                return true;
            }

            if (visited.Contains(actionId))
                return false;

            visited.Add(actionId);
            recursionStack.Add(actionId);

            var action = allActions.FirstOrDefault(a => a.Id == actionId);
            if (action != null)
            {
                var executeActions = GetAllExecuteActionSubActions(action.SubActions);
                foreach (var executeAction in executeActions)
                {
                    if (executeAction.ActionId.HasValue && 
                        executeAction.ActionId.Value != Guid.Empty &&
                        HasCircularDependency(executeAction.ActionId.Value, allActions, visited, recursionStack, out var nestedPath))
                    {
                        cyclePath = $"{actionId} -> {nestedPath}";
                        return true;
                    }
                }
            }

            recursionStack.Remove(actionId);
            return false;
        }

        private List<ExecuteActionType> GetAllExecuteActionSubActions(List<SubActionType> subActions)
        {
            var result = new List<ExecuteActionType>();
            foreach (var subAction in subActions)
            {
                if (subAction is ExecuteActionType executeAction)
                {
                    result.Add(executeAction);
                }
                else if (subAction is LogicIfElseType logicIfElse)
                {
                    if (logicIfElse.TrueSubActions != null)
                        result.AddRange(GetAllExecuteActionSubActions(logicIfElse.TrueSubActions));
                    if (logicIfElse.FalseSubActions != null)
                        result.AddRange(GetAllExecuteActionSubActions(logicIfElse.FalseSubActions));
                }
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
            result.Stats["Triggers"] = allTriggers.Count;

            // Get all timer groups and commands for validation - with full entities for name validation
            var timerGroups = await db.TimerGroups.GetAsync();
            var timerGroupDict = timerGroups
                .Where(t => t.Id != Guid.Empty)
                .ToDictionary(t => t.Id, t => t);

            var commands = await db.ActionCommands.GetAsync();
            var commandDict = commands
                .Where(c => c.Id != Guid.Empty)
                .ToDictionary(c => c.Id, c => c);

            var keywords = await db.ActionKeywords.GetAsync();
            var keywordDict = keywords
                .Where(k => k.Id != Guid.Empty)
                .ToDictionary(k => k.Id, k => k);

            // Get all actions to check for orphaned triggers
            var allActions = await db.Actions.GetAllWithDetailsAsync();
            var actionIds = new HashSet<Guid>(allActions.Where(a => a.Id != Guid.Empty).Select(a => a.Id));

            // Track duplicate triggers per action
            var triggersByAction = allTriggers
                .Where(t => t.ActionId.HasValue && t.ActionId.Value != Guid.Empty)
                .GroupBy(t => t.ActionId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var trigger in allTriggers)
            {
                // Validate trigger has an action ID
                if (!trigger.ActionId.HasValue)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerOrphaned,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"Trigger '{trigger.Name}' has no associated ActionId"
                    });
                    continue;
                }

                // Validate the action exists
                if (!actionIds.Contains(trigger.ActionId.Value))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerOrphaned,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"Trigger '{trigger.Name}' references non-existent Action ID: {trigger.ActionId}",
                        RelatedActionId = trigger.ActionId,
                        RelatedActionName = trigger.Action?.Name
                    });
                }

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

                // Validate Keyword triggers
                if (trigger.Type == TriggerTypes.Keyword)
                {
                    ValidateKeywordTrigger(trigger, keywordDict, result);
                }

                // Validate TwitchEvent triggers
                if (trigger.Type == TriggerTypes.TwitchEvent)
                {
                    ValidateTwitchEventTrigger(trigger, result);
                }
            }

            // Check for duplicate triggers within the same action
            foreach (var kvp in triggersByAction)
            {
                var actionId = kvp.Key;
                var triggers = kvp.Value;

                var action = allActions.FirstOrDefault(a => a.Id == actionId);
                var actionName = action?.Name ?? $"Action ID {actionId}";

                // Group triggers by type and name to find exact duplicates
                var duplicateGroups = triggers
                    .GroupBy(t => new { t.Type, t.Name })
                    .Where(g => g.Count() > 1);

                foreach (var group in duplicateGroups)
                {
                    var triggerIds = string.Join(", ", group.Select(t => t.Id));
                    foreach (var trigger in group)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.TriggerDuplicate,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "Trigger",
                            EntityId = trigger.Id,
                            EntityName = trigger.Name,
                            Message = $"Duplicate {trigger.Type} trigger '{trigger.Name}' found for action '{actionName}' (Trigger IDs: {triggerIds})",
                            RelatedActionId = actionId,
                            RelatedActionName = actionName
                        });
                    }
                }
            }

            _logger.LogDebug("Trigger validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        private void ValidateTwitchEventTrigger(TriggerType trigger, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(trigger.Configuration))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"TwitchEvent trigger '{trigger.Name}' has missing Configuration JSON data",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
                return;
            }

            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(trigger.Configuration);
                if (config == null || !config.ContainsKey("EventName"))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TriggerInvalidConfiguration,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Trigger",
                        EntityId = trigger.Id,
                        EntityName = trigger.Name,
                        Message = $"TwitchEvent trigger '{trigger.Name}' has invalid Configuration JSON (missing EventName)",
                        RelatedActionId = trigger.ActionId,
                        RelatedActionName = trigger.Action?.Name
                    });
                }
            }
            catch (Exception)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerInvalidConfiguration,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"TwitchEvent trigger '{trigger.Name}' has malformed Configuration JSON",
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }
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
            result.Stats["Sub-actions"] = allActions.Sum(a => Flatten(a.SubActions).Count() + Flatten(a.CatchSubActions).Count());

            // Get all commands and actions for validation
            var commands = await db.ActionCommands.GetAsync();
            var commandIds = new HashSet<Guid>(commands.Where(c => c.Id != Guid.Empty).Select(c => c.Id));
            var commandNames = new HashSet<string>(commands.Select(c => c.CommandName), StringComparer.OrdinalIgnoreCase);

            var actionIds = new HashSet<Guid>(allActions.Where(a => a.Id != Guid.Empty).Select(a => a.Id));

            // Get all default commands for ExecuteDefaultCommand validation
            var defaultCommands = await db.DefaultCommands.GetAllAsync();
            var defaultCommandNames = new HashSet<string>(defaultCommands.Select(dc => dc.CommandName), StringComparer.OrdinalIgnoreCase);

            // Get all point types for CheckPoints validation
            var pointTypes = await pointsSystem.GetPointTypes();
            var pointTypeNames = new HashSet<string>(pointTypes.Select(pt => pt.Name), StringComparer.OrdinalIgnoreCase);

            // Get all timer groups for TimerGroupSetEnabledState validation
            var timerGroups = await db.TimerGroups.GetAsync();
            var timerGroupIds = new HashSet<Guid>(timerGroups.Where(tg => tg.Id != Guid.Empty).Select(tg => tg.Id));

            foreach (var action in allActions)
            {
                ValidateSubActionList(
                    action.SubActions, 
                    commandIds, 
                    commandNames, 
                    actionIds, 
                    defaultCommandNames, 
                    pointTypeNames,
                    timerGroupIds,
                    result, 
                    action.Id, 
                    action.Name);
            }

            _logger.LogDebug("SubAction validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        private void ValidateSubActionList(
            List<SubActionType> subActions, 
            HashSet<Guid> commandIds,
            HashSet<string> commandNames,
            HashSet<Guid> actionIds,
            HashSet<string> defaultCommandNames,
            HashSet<string> pointTypeNames,
            HashSet<Guid> timerGroupIds,
            ValidationResult result,
            Guid? parentActionId,
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
                    else if (string.IsNullOrWhiteSpace(toggleCommand.CommandName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionCommandNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Toggle Command Disabled",
                            Message = "SubAction 'Toggle Command Disabled' has missing or empty CommandName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ExecuteAction subactions
                if (subAction is ExecuteActionType executeAction)
                {
                    if (!executeAction.ActionId.HasValue || executeAction.ActionId.Value == Guid.Empty)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionActionNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Execute Action",
                            Message = "SubAction 'Execute Action' has missing or invalid ActionId",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (!actionIds.Contains(executeAction.ActionId.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionActionNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Execute Action",
                            Message = $"SubAction 'Execute Action' references non-existent Action ID: {executeAction.ActionId}",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ForEachViewer subactions
                if (subAction is ForEachViewerType forEachViewer)
                {
                    if (!forEachViewer.ActionId.HasValue || forEachViewer.ActionId.Value == Guid.Empty)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionActionNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "For Each Viewer",
                            Message = "SubAction 'For Each Viewer' has missing or invalid ActionId",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (!actionIds.Contains(forEachViewer.ActionId.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionActionNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "For Each Viewer",
                            Message = $"SubAction 'For Each Viewer' references non-existent Action ID: {forEachViewer.ActionId}",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ExecuteDefaultCommand subactions
                if (subAction is ExecuteDefaultCommandType executeDefaultCommand)
                {
                    if (string.IsNullOrWhiteSpace(executeDefaultCommand.CommandName))
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
                    else if (!defaultCommandNames.Contains(executeDefaultCommand.CommandName))
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
                }

                // Validate CheckPoints subactions
                if (subAction is CheckPointsType checkPoints)
                {
                    if (string.IsNullOrWhiteSpace(checkPoints.PointTypeName))
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
                    else if (!pointTypeNames.Contains(checkPoints.PointTypeName))
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

                    if (string.IsNullOrWhiteSpace(checkPoints.TargetUser))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Check Points",
                            Message = "SubAction 'Check Points' has missing or empty TargetUser",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate TimerGroupSetEnabledState subactions
                if (subAction is TimerGroupSetEnabledStateType timerGroupSetEnabled)
                {
                    if (!timerGroupSetEnabled.TimerGroupId.HasValue || timerGroupSetEnabled.TimerGroupId.Value == Guid.Empty)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionTimerGroupNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Toggle Timer Group Enabled",
                            Message = "SubAction 'Toggle Timer Group Enabled' has missing or invalid TimerGroupId",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (!timerGroupIds.Contains(timerGroupSetEnabled.TimerGroupId.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionTimerGroupNotFound,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Toggle Timer Group Enabled",
                            Message = $"SubAction 'Toggle Timer Group Enabled' references non-existent Timer Group ID: {timerGroupSetEnabled.TimerGroupId}",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ChannelPointSetEnabledState and ChannelPointSetPausedState subactions
                if (subAction is ChannelPointSetEnabledStateType channelPointEnabled)
                {
                    if (string.IsNullOrWhiteSpace(channelPointEnabled.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Set Channel Point Enabled State",
                            Message = "SubAction 'Set Channel Point Enabled State' has missing or empty reward name",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                if (subAction is ChannelPointSetPausedStateType channelPointPaused)
                {
                    if (string.IsNullOrWhiteSpace(channelPointPaused.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Set Channel Point Paused State",
                            Message = "SubAction 'Set Channel Point Paused State' has missing or empty reward name",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate PlaySound subactions
                if (subAction is PlaySoundType playSound)
                {
                    if (string.IsNullOrWhiteSpace(playSound.File))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Play Sound",
                            Message = "SubAction 'Play Sound' has missing or empty file path",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate Alert subactions
                if (subAction is AlertType alert)
                {
                    if (string.IsNullOrWhiteSpace(alert.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Alert",
                            Message = "SubAction 'Alert' has missing or empty alert text",
                            Detail = "Alert.Text",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (alert.Duration < 1 || alert.Duration > 60)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Alert",
                            Message = $"SubAction 'Alert' has invalid duration: {alert.Duration} (should be between 1 and 60 seconds)",
                            Detail = "Alert.Duration",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (alert.Volume < 0 || alert.Volume > 1)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Alert",
                            Message = $"SubAction 'Alert' has invalid volume: {alert.Volume} (should be between 0 and 1)",
                            Detail = "Alert.Volume",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ExternalApi subactions
                if (subAction is ExternalApiType externalApi)
                {
                    if (string.IsNullOrWhiteSpace(externalApi.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "External API",
                            Message = "SubAction 'External API' has missing or empty URL",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (!IsValidUrlFormat(externalApi.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "External API",
                            Message = $"SubAction 'External API' has potentially invalid URL format: {externalApi.Text}",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate WriteFile subactions
                if (subAction is WriteFileType writeFile)
                {
                    if (string.IsNullOrWhiteSpace(writeFile.File))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Write File",
                            Message = "SubAction 'Write File' has missing or empty file path",
                            Detail = "WriteFile.File",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (string.IsNullOrWhiteSpace(writeFile.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Write File",
                            Message = "SubAction 'Write File' has empty content (will write empty string to file)",
                            Detail = "WriteFile.Text",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate LogicIfElse subactions
                if (subAction is LogicIfElseType logicIfElse)
                {
                    if (string.IsNullOrWhiteSpace(logicIfElse.LeftValue))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Logic: If/Else",
                            Message = "SubAction 'Logic: If/Else' has missing or empty Left Value",
                            Detail = "LogicIfElse.LeftValue",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (string.IsNullOrWhiteSpace(logicIfElse.RightValue))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Logic: If/Else",
                            Message = "SubAction 'Logic: If/Else' has missing or empty Right Value",
                            Detail = "LogicIfElse.RightValue",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    // Recursively validate TrueSubActions
                    if (logicIfElse.TrueSubActions?.Count > 0)
                    {
                        ValidateSubActionList(
                            logicIfElse.TrueSubActions, 
                            commandIds, 
                            commandNames, 
                            actionIds, 
                            defaultCommandNames, 
                            pointTypeNames, 
                            timerGroupIds,
                            result, 
                            parentActionId, 
                            parentActionName);
                    }

                    // Recursively validate FalseSubActions
                    if (logicIfElse.FalseSubActions?.Count > 0)
                    {
                        ValidateSubActionList(
                            logicIfElse.FalseSubActions, 
                            commandIds, 
                            commandNames, 
                            actionIds, 
                            defaultCommandNames, 
                            pointTypeNames,
                            timerGroupIds,
                            result, 
                            parentActionId, 
                            parentActionName);
                    }
                }

                // Validate Delay subactions
                if (subAction is DelayType delay)
                {
                    if (string.IsNullOrWhiteSpace(delay.Duration))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Delay",
                            Message = "SubAction 'Delay' has missing or empty duration",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                    else if (delay.Duration.Contains(' '))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Delay",
                            Message = "SubAction 'Delay' duration contains spaces",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate SetVariable subactions
                if (subAction is SetVariableType setVariable)
                {
                    if (string.IsNullOrWhiteSpace(setVariable.Text))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Set Variable",
                            Message = "SubAction 'Set Variable' has missing or empty variable name",
                            Detail = "SetVariable.VariableName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (string.IsNullOrWhiteSpace(setVariable.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Warning,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "Set Variable",
                            Message = "SubAction 'Set Variable' has empty value (will set variable to empty string)",
                            Detail = "SetVariable.Value",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ObsSetScene subactions
                if (subAction is ObsSetSceneType obsSetScene)
                {
                    if (string.IsNullOrWhiteSpace(obsSetScene.SceneName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "OBS - Set Scene",
                            Message = "SubAction 'OBS - Set Scene' has missing or empty scene name",
                            Detail = "ObsSetScene.SceneName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }

                // Validate ObsSetSceneFilterState subactions
                if (subAction is ObsSetSceneFilterStateType obsSetFilter)
                {
                    if (string.IsNullOrWhiteSpace(obsSetFilter.SceneName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "OBS - Set Scene Filter State",
                            Message = "SubAction 'OBS - Set Scene Filter State' has missing or empty scene name",
                            Detail = "ObsSetSceneFilterState.SceneName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }

                    if (string.IsNullOrWhiteSpace(obsSetFilter.FilterName))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            IssueType = ValidationIssueType.SubActionInvalidConfiguration,
                            Severity = ValidationSeverity.Error,
                            EntityType = "SubAction",
                            EntityId = subAction.Id,
                            EntityName = "OBS - Set Scene Filter State",
                            Message = "SubAction 'OBS - Set Scene Filter State' has missing or empty filter name",
                            Detail = "ObsSetSceneFilterState.FilterName",
                            RelatedActionId = parentActionId,
                            RelatedActionName = parentActionName
                        });
                    }
                }
            }
        }

        private bool IsValidUrlFormat(string url)
        {
            // Allow variables like %user% in URL
            if (url.Contains('%'))
            {
                // Create a test URL by replacing variables with dummy values
                var testUrl = System.Text.RegularExpressions.Regex.Replace(url, @"%\w+%", "test");
                return Uri.TryCreate(testUrl, UriKind.Absolute, out var uriResult) &&
                       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        public async Task MarkEntityWithIssueAsync(string entityType, Guid entityId, bool hasIssue)
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

        // Command, keyword and timer triggers are linked through their id column only, so that is all that needs checking.
        private static void ValidateTimerTrigger(TriggerType trigger, Dictionary<Guid, TimerGroup> timerGroupDict, ValidationResult result) =>
            ValidateReference(trigger, trigger.TimerGroupId, "Timer", "timer group", timerGroupDict.ContainsKey,
                ValidationIssueType.TriggerTimerGroupNotFound, "Choose a timer group on the trigger or remove it.", result);

        private static void ValidateKeywordTrigger(TriggerType trigger, Dictionary<Guid, ActionKeyword> keywordDict, ValidationResult result) =>
            ValidateReference(trigger, trigger.KeywordId, "Keyword", "keyword", keywordDict.ContainsKey,
                ValidationIssueType.TriggerKeywordNotFound, "Choose a keyword on the trigger or remove it.", result);

        private static void ValidateCommandTrigger(TriggerType trigger, Dictionary<Guid, ActionCommand> commandDict, ValidationResult result) =>
            ValidateReference(trigger, trigger.CommandId, "Command", "command", commandDict.ContainsKey,
                ValidationIssueType.TriggerCommandNotFound, "Choose a command on the trigger or remove it.", result);

        private static void ValidateReference(
            TriggerType trigger,
            Guid? referenceId,
            string triggerLabel,
            string targetLabel,
            Func<Guid, bool> exists,
            ValidationIssueType notFoundType,
            string suggestion,
            ValidationResult result)
        {
            if (!referenceId.HasValue || referenceId.Value == Guid.Empty)
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.TriggerMissingConfiguration,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"{triggerLabel} trigger '{trigger.Name}' is not linked to a {targetLabel}.",
                    Suggestion = suggestion,
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }
            else if (!exists(referenceId.Value))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = notFoundType,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Trigger",
                    EntityId = trigger.Id,
                    EntityName = trigger.Name,
                    Message = $"{triggerLabel} trigger '{trigger.Name}' points at a {targetLabel} that no longer exists.",
                    Suggestion = suggestion,
                    RelatedActionId = trigger.ActionId,
                    RelatedActionName = trigger.Action?.Name
                });
            }
        }

        public async Task<ValidationResult> ValidateCommandsAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get all commands
            var commands = await db.ActionCommands.GetAsync();
            _logger.LogDebug("Validating {CommandCount} commands", commands.Count);
            result.Stats["Commands"] = commands.Count;

            // Get all triggers to check which commands are referenced
            var allTriggers = await db.Triggers.GetAllAsync();
            var commandsReferencedByTriggers = new HashSet<Guid>(
                allTriggers
                    .Where(t => t.Type == TriggerTypes.Command && t.CommandId.HasValue && t.CommandId.Value != Guid.Empty)
                    .Select(t => t.CommandId!.Value));

            foreach (var command in commands)
            {
                // Validate command name
                if (string.IsNullOrWhiteSpace(command.CommandName))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.CommandInvalidName,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Command",
                        EntityId = command.Id,
                        EntityName = command.CommandName ?? "(unnamed)",
                        Message = $"Command (ID: {command.Id}) has an invalid or missing CommandName"
                    });
                }

                // Warn about orphaned commands (not referenced by any trigger)
                if (command.Id != Guid.Empty && !commandsReferencedByTriggers.Contains(command.Id))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.CommandNoTriggers,
                        Severity = ValidationSeverity.Info,
                        EntityType = "Command",
                        EntityId = command.Id,
                        EntityName = command.CommandName ?? "(unknown)",
                        Message = $"Command '!{command.CommandName}' is not used by any action trigger"
                    });
                }
            }

            _logger.LogDebug("Command validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        public async Task<ValidationResult> ValidateKeywordsAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get all keywords
            var keywords = await db.ActionKeywords.GetAsync();
            _logger.LogDebug("Validating {KeywordCount} keywords", keywords.Count);
            result.Stats["Keywords"] = keywords.Count;

            // Get all triggers to check which keywords are referenced
            var allTriggers = await db.Triggers.GetAllAsync();
            var keywordsReferencedByTriggers = new HashSet<Guid>(
                allTriggers
                    .Where(t => t.Type == TriggerTypes.Keyword && t.KeywordId.HasValue && t.KeywordId.Value != Guid.Empty)
                    .Select(t => t.KeywordId!.Value));

            foreach (var keyword in keywords)
            {
                // Validate keyword pattern
                if (string.IsNullOrWhiteSpace(keyword.CommandName))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.KeywordInvalidPattern,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Keyword",
                        EntityId = keyword.Id,
                        EntityName = keyword.CommandName ?? "(unnamed)",
                        Message = $"Keyword (ID: {keyword.Id}) has an invalid or missing Pattern"
                    });
                }

                // Warn about orphaned keywords (not referenced by any trigger)
                if (keyword.Id != Guid.Empty && !keywordsReferencedByTriggers.Contains(keyword.Id))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.KeywordNoTriggers,
                        Severity = ValidationSeverity.Info,
                        EntityType = "Keyword",
                        EntityId = keyword.Id,
                        EntityName = keyword.CommandName ?? "(unknown)",
                        Message = $"Keyword '{keyword.CommandName}' is not used by any action trigger"
                    });
                }
            }

            _logger.LogDebug("Keyword validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }
    }
}

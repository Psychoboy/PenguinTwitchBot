using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ActionsRepository : GenericRepository<ActionType>, IActionsRepository
    {
        public ActionsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ActionType?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<ActionType>> GetAllWithDetailsAsync()
        {
            return await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<ActionType> CreateActionAsync(ActionType action)
        {
            // Assign IDs to new SubActions
            if (action.SubActions != null && action.SubActions.Any())
            {
                var maxId = await _context.SubActions.MaxAsync(s => (int?)s.Id) ?? 0;
                foreach (var subAction in action.SubActions.Where(s => s.Id == 0))
                {
                    maxId++;
                    subAction.Id = maxId;
                }
            }

            // Populate ActionName for ExecuteActionType subactions before save
            if(action.SubActions != null)
            {
                await PopulateExecuteActionNamesBeforeSave(action.SubActions);
                await PopulateTimerGroupNamesBeforeSave(action.SubActions);
            }

            await _context.Actions.AddAsync(action);
            await _context.SaveChangesAsync();

            return action;
        }

        public async Task<ActionType> UpdateActionAsync(ActionType action)
        {
            if (!action.Id.HasValue || action.Id.Value == 0)
            {
                throw new InvalidOperationException("Cannot update action: Action ID is null or zero");
            }

            // Load the existing action from database WITH tracking
            var existingAction = await _context.Actions
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == action.Id.Value);

            if (existingAction == null)
            {
                throw new InvalidOperationException($"Action with ID {action.Id.Value} not found");
            }

            // Update scalar properties on the tracked entity
            _context.Entry(existingAction).CurrentValues.SetValues(action);

            // Handle SubActions: Remove deleted, Add new, Update existing
            var existingSubActionIds = existingAction.SubActions.Select(s => s.Id).ToHashSet();
            var newSubActionIds = action.SubActions.Where(s => s.Id > 0).Select(s => s.Id).ToHashSet();

            // Remove SubActions that are no longer in the incoming list
            var subActionsToRemove = existingAction.SubActions
                .Where(s => !newSubActionIds.Contains(s.Id))
                .ToList();
            foreach (var subAction in subActionsToRemove)
            {
                existingAction.SubActions.Remove(subAction);
                _context.Entry(subAction).State = EntityState.Deleted;
            }

            // Process incoming SubActions
            // Track the next available ID to prevent duplicate ID assignments in the same operation
            var nextAvailableId = await _context.SubActions.MaxAsync(s => (int?)s.Id) ?? 0;

            foreach (var subAction in action.SubActions)
            {
                // Check if this is a new SubAction (Id = 0 or not in existing)
                if (subAction.Id == 0 || !existingSubActionIds.Contains(subAction.Id))
                {
                    // New SubAction - generate ID
                    if (subAction.Id == 0)
                    {
                        // Assign the next available ID and increment for subsequent subactions
                        nextAvailableId++;
                        subAction.Id = nextAvailableId;
                    }

                    // Add to collection - EF will INSERT this
                    existingAction.SubActions.Add(subAction);
                }
                else
                {
                    // Existing SubAction - find and update it
                    var existingSubAction = existingAction.SubActions.FirstOrDefault(s => s.Id == subAction.Id);
                    if (existingSubAction != null)
                    {
                        // Remove the old one and add the new one (for polymorphism)
                        existingAction.SubActions.Remove(existingSubAction);
                        _context.Entry(existingSubAction).State = EntityState.Deleted;
                        existingAction.SubActions.Add(subAction);
                    }
                }
            }

            // Handle Triggers: Remove deleted, Add new, Update existing
            var existingTriggerIds = existingAction.Triggers.Select(t => t.Id).ToHashSet();
            var newTriggerIds = action.Triggers.Where(t => t.Id > 0).Select(t => t.Id).ToHashSet();

            // Remove Triggers that are no longer in the incoming list
            var triggersToRemove = existingAction.Triggers
                .Where(t => !newTriggerIds.Contains(t.Id))
                .ToList();
            foreach (var trigger in triggersToRemove)
            {
                existingAction.Triggers.Remove(trigger);
                _context.Entry(trigger).State = EntityState.Deleted;
            }

            // Update existing Triggers (Enabled, Priority, etc.)
            foreach (var trigger in action.Triggers)
            {
                var existing = existingAction.Triggers.FirstOrDefault(t => t.Id == trigger.Id);
                if (existing != null)
                {
                    _context.Entry(existing).CurrentValues.SetValues(trigger);
                }
                else if (!existingTriggerIds.Contains(trigger.Id))
                {
                    // New Trigger - add to collection
                    trigger.ActionId = action.Id.Value;
                    existingAction.Triggers.Add(trigger);
                }
            }

            // Populate ActionName for ExecuteActionType subactions before save
            await PopulateExecuteActionNamesBeforeSave(existingAction.SubActions);
            await PopulateTimerGroupNamesBeforeSave(existingAction.SubActions);

            await _context.SaveChangesAsync();

            return existingAction;
        }

        public async Task DeleteActionAsync(int id)
        {
            var action = await _context.Actions.FindAsync(id);
            if (action != null)
            {
                _context.Actions.Remove(action);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Populates ActionName for all ExecuteActionType subactions (including nested) before save.
        /// Only queries actions that are actually referenced.
        /// </summary>
        private async Task PopulateExecuteActionNamesBeforeSave(List<SubActionType> subActions)
        {
            var allExecuteActions = GetAllExecuteActionSubActions(subActions);
            if (!allExecuteActions.Any()) return;

            // Get distinct ActionIds that need to be resolved
            var actionIdsToResolve = allExecuteActions.Select(e => e.ActionId).Distinct().ToList();

            // Query only the actions we need
            var referencedActions = await _context.Actions
                .AsNoTracking()
                .Where(a => a.Id.HasValue && actionIdsToResolve.Contains(a.Id.Value))
                .Select(a => new { a.Id, a.Name })
                .ToListAsync();

            var actionIdToNameMap = referencedActions.ToDictionary(a => a.Id!.Value, a => a.Name);

            // Populate ActionName for each ExecuteAction subaction
            foreach (var executeAction in allExecuteActions)
            {
                if (executeAction.ActionId.HasValue && actionIdToNameMap.TryGetValue(executeAction.ActionId.Value, out var actionName))
                {
                    executeAction.ActionName = actionName;
                }
            }
        }

        /// <summary>
        /// Recursively finds all ExecuteActionType subactions, including those nested in LogicIfElseType.
        /// </summary>
        private List<ExecuteActionType> GetAllExecuteActionSubActions(IEnumerable<SubActionType> subActions)
        {
            var result = new List<ExecuteActionType>();
            foreach (var subAction in subActions)
            {
                if (subAction is ExecuteActionType exec)
                {
                    result.Add(exec);
                }
                else if (subAction is LogicIfElseType logic)
                {
                    result.AddRange(GetAllExecuteActionSubActions(logic.TrueSubActions));
                    result.AddRange(GetAllExecuteActionSubActions(logic.FalseSubActions));
                }
            }
            return result;
        }

        /// <summary>
        /// Populates TimerGroupName for all TimerGroupSetEnabledStateType subactions (including nested) before save.
        /// Only queries timer groups that are actually referenced.
        /// </summary>
        private async Task PopulateTimerGroupNamesBeforeSave(List<SubActionType> subActions)
        {
            var allTimerGroupSubActions = GetAllTimerGroupSubActions(subActions);
            if (!allTimerGroupSubActions.Any()) return;

            // Get distinct TimerGroupIds that need to be resolved
            var timerGroupIdsToResolve = allTimerGroupSubActions
                .Where(tg => tg.TimerGroupId.HasValue)
                .Select(tg => tg.TimerGroupId!.Value)
                .Distinct()
                .ToList();

            if (!timerGroupIdsToResolve.Any()) return;

            // Query only the timer groups we need
            var referencedTimerGroups = await _context.Set<Bot.Models.Timers.TimerGroup>()
                .AsNoTracking()
                .Where(tg => tg.Id.HasValue && timerGroupIdsToResolve.Contains(tg.Id.Value))
                .Select(tg => new { tg.Id, tg.Name })
                .ToListAsync();

            var timerGroupIdToNameMap = referencedTimerGroups.ToDictionary(tg => tg.Id!.Value, tg => tg.Name);

            // Populate TimerGroupName for each TimerGroupSetEnabledState subaction
            foreach (var timerGroupSubAction in allTimerGroupSubActions)
            {
                if (timerGroupSubAction.TimerGroupId.HasValue &&
                    timerGroupIdToNameMap.TryGetValue(timerGroupSubAction.TimerGroupId.Value, out var timerGroupName))
                {
                    timerGroupSubAction.TimerGroupName = timerGroupName;
                }
            }
        }

        public async Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName)
        {
            return await _context.Actions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .Where(a => a.Triggers.Any(t => 
                    t.Type == triggerType && 
                    t.Name == triggerName &&
                    t.Enabled))
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // Load Actions with all related SubActions and Triggers
            var records = await context.Set<ActionType>()
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .ToListAsync();

            // Populate ActionName for all ExecuteAction subactions (including nested) based on ActionId
            var actionIdToNameMap = records.ToDictionary(a => a.Id!.Value, a => a.Name);
            foreach (var action in records)
            {
                var allExecuteActions = GetAllExecuteActionSubActions(action.SubActions);
                foreach (var subAction in allExecuteActions)
                {
                    if (subAction.ActionId.HasValue && actionIdToNameMap.TryGetValue(subAction.ActionId.Value, out var actionName))
                    {
                        subAction.ActionName = actionName;
                    }
                }
            }

            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = true,
                TypeInfoResolver = new SubActionTypeResolver()
            };

            var json = JsonSerializer.Serialize(records, options);

            var fileName = $"{backupDirectory}/ActionType.json";
            await File.WriteAllTextAsync(fileName, json, encoding: System.Text.Encoding.UTF8);
            logger?.LogDebug("Backed up {Count} ActionType records with SubActions and Triggers", records.Count);
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/ActionType.json";
                if (!File.Exists(fileName)) return;

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    TypeInfoResolver = new SubActionTypeResolver()
                };

                var records = JsonSerializer.Deserialize<List<ActionType>>(json, options);
                if (records == null) throw new Exception("ActionType.json was null");

                logger?.LogDebug("Deserialized {Count} ActionType records", records.Count);

                context.ChangeTracker.Clear();

                // ExecuteDeleteAsync bypasses EF cascade deletes, so we must explicitly delete subaction tables first
                // Get all subaction table names from the SubActionRegistry
                var subActionTableNames = SubActionRegistry.Metadata.Values
                    .Select(m => m.TableName)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .ToList();

                // Delete all subaction tables
                foreach (var tableName in subActionTableNames)
                {
                    await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{tableName}`");
                }

                // Delete Triggers (they have FK to Actions)
                await context.Database.ExecuteSqlRawAsync("DELETE FROM `Triggers`");

                // Finally, delete Actions
                await context.Set<ActionType>().ExecuteDeleteAsync();

                context.ChangeTracker.Clear();

                // SubAction IDs are marked with [JsonIgnore], so they're not in the backup
                // We need to assign IDs manually before adding to context
                int nextSubActionId = 1; // Start from 1 since table is empty


                // First Pass: Add actions with non-ExecuteAction subactions (including nested)
                // ExecuteAction subactions need to be added after all actions are created
                // so we can map ActionName back to ActionId
                var executeActionSubActions = new List<(ActionType action, ExecuteActionType subAction, List<SubActionType> parentList)>();

                foreach (var record in records)
                {
                    // Recursively remove all ExecuteAction subactions and collect them for second pass
                    RemoveAndCollectExecuteActions(record.SubActions, record, executeActionSubActions);

                    // Assign IDs to remaining SubActions
                    foreach (var subAction in record.SubActions)
                    {
                        subAction.Id = nextSubActionId++;
                    }

                    // Ensure ActionId is set for Triggers
                    foreach (var trigger in record.Triggers)
                    {
                        if (record.Id.HasValue)
                        {
                            trigger.ActionId = record.Id.Value;
                        }
                    }

                    // Add the Action - EF will cascade to SubActions and Triggers
                    context.Set<ActionType>().Add(record);
                }

                // Save to generate new Action IDs
                await context.SaveChangesAsync();
                logger?.LogDebug("Restored {ActionCount} Actions (first pass)", records.Count);

                // Second Pass: Add ExecuteAction subactions with resolved ActionIds
                if (executeActionSubActions.Any())
                {
                    // Build a map from action name to new action ID
                    // Handle duplicate names by using GroupBy and taking the first
                    var actionNameToIdMap = records
                        .Where(a => a.Id.HasValue)
                        .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => 
                            {
                                if (g.Count() > 1)
                                {
                                    logger?.LogWarning("Multiple actions found with name '{Name}' (case-insensitive). Using first occurrence (ID: {Id})", g.Key, g.First().Id);
                                }
                                return g.First().Id!.Value;
                            },
                            StringComparer.OrdinalIgnoreCase);

                    foreach (var (action, executeSubAction, parentList) in executeActionSubActions)
                    {
                        // Resolve ActionId from ActionName
                        if (actionNameToIdMap.TryGetValue(executeSubAction.ActionName, out var resolvedActionId))
                        {
                            executeSubAction.ActionId = resolvedActionId;
                            executeSubAction.Id = nextSubActionId++;
                            parentList.Add(executeSubAction);
                        }
                        else
                        {
                            logger?.LogWarning("ExecuteAction subaction references unknown action: {ActionName}", executeSubAction.ActionName);
                        }
                    }

                        // Save ExecuteAction subactions
                        await context.SaveChangesAsync();
                        logger?.LogDebug("Restored {ExecuteActionCount} ExecuteAction subactions (second pass)", 
                            executeActionSubActions.Count);
                    }

                    var subActionCount = records.Sum(r => r.SubActions.Count);
                    var triggerCount = records.Sum(r => r.Triggers.Count);
                    logger?.LogDebug("Restored {ActionCount} Actions with {SubActionCount} SubActions and {TriggerCount} Triggers", 
                        records.Count, subActionCount, triggerCount);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore ActionType");
                throw;
            }
        }

        /// <summary>
        /// Post-restore method to remap all entity references after all tables have been restored.
        /// This must be called AFTER all tables are restored so TimerGroups and ActionCommands have their new IDs.
        /// </summary>
        public async Task RemapEntityReferencesAfterRestore(ILogger? logger = null)
        {
            logger?.LogInformation("Starting post-restore entity reference remapping...");

            // Load all actions with triggers and subactions
            var actions = await _context.Actions
                .Include(a => a.Triggers)
                .Include(a => a.SubActions)
                .AsSplitQuery()
                .ToListAsync();

            // Third Pass: Remap Timer trigger IDs based on TimerGroupName
            await RemapTimerTriggerIds(_context, actions, logger);

            // Fourth Pass: Remap Command trigger IDs based on CommandName
            await RemapCommandTriggerIds(_context, actions, logger);

            // Fifth Pass: Remap TimerGroupSetEnabledState SubAction IDs based on TimerGroupName
            await RemapTimerGroupSubActionIds(_context, actions, logger);

            // Sixth Pass: Validate ToggleCommandDisabled SubAction CommandNames
            await RemapToggleCommandDisabledSubActions(_context, actions, logger);

            logger?.LogInformation("Completed post-restore entity reference remapping");
        }

        /// <summary>
        /// Recursively removes all ExecuteActionType subactions from the given list and collects them for later processing.
        /// Also processes nested LogicIfElseType subactions.
        /// </summary>
        private void RemoveAndCollectExecuteActions(List<SubActionType> subActions, ActionType action, List<(ActionType, ExecuteActionType, List<SubActionType>)> collected)
        {
            for (int i = subActions.Count - 1; i >= 0; i--)
            {
                var subAction = subActions[i];
                if (subAction is ExecuteActionType exec)
                {
                    collected.Add((action, exec, subActions));
                    subActions.RemoveAt(i);
                }
                else if (subAction is LogicIfElseType logic)
                {
                    RemoveAndCollectExecuteActions(logic.TrueSubActions, action, collected);
                    RemoveAndCollectExecuteActions(logic.FalseSubActions, action, collected);
                }
            }
        }

        /// <summary>
        /// Updates ActionName for all ExecuteActionType subactions (including nested) referencing the given actionId.
        /// Optimized to only load actions that contain ExecuteAction subactions.
        /// </summary>
        public async Task UpdateExecuteActionNamesForRenamedAction(int actionId, string newName)
        {
            // Load all actions with their subactions
            // Note: We need to load all because we can't efficiently filter by nested ExecuteActionType in a query
            var allActions = await _context.Actions
                .Include(a => a.SubActions)
                .ToListAsync();

            bool hasChanges = false;
            foreach (var action in allActions)
            {
                var allExecuteActions = GetAllExecuteActionSubActions(action.SubActions);
                foreach (var exec in allExecuteActions)
                {
                    if (exec.ActionId == actionId && exec.ActionName != newName)
                    {
                        exec.ActionName = newName;
                        hasChanges = true;
                    }
                }
            }
            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates CommandName in all Command trigger configurations when a command is renamed.
        /// </summary>
        public async Task UpdateCommandTriggerConfigurationsForRenamedCommand(int commandId, string oldCommandName, string newCommandName)
        {


            var commandTriggers = await _context.Triggers
                .Where(x => x.Type == TriggerTypes.Command && x.Name == "!" + oldCommandName).ToListAsync();

            var updatedCount = 0;
            foreach (var trigger in commandTriggers)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(trigger.Configuration);
                    if (config == null) continue;
                    // Update the CommandName in the configuration
                    var newConfig = new
                    {
                        CommandId = commandId,
                        CommandName = newCommandName
                    };
                    trigger.Configuration = JsonSerializer.Serialize(newConfig);
                    trigger.Name = "!" + newCommandName;

                    // Update the reference column as well
                    trigger.CommandId = commandId;

                    // Explicitly mark the properties as modified
                    _context.Entry(trigger).Property(t => t.Configuration).IsModified = true;
                    _context.Entry(trigger).Property(t => t.Name).IsModified = true;
                    _context.Entry(trigger).Property(t => t.CommandId).IsModified = true;
                    updatedCount++;
                }
                catch (Exception)
                {
                    // Skip triggers with invalid configuration
                    continue;
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates TimerGroupName for all TimerGroupSetEnabledStateType subactions (including nested) referencing the given timerGroupId.
        /// </summary>
        public async Task UpdateTimerGroupNamesForRenamedTimerGroup(int timerGroupId, string newName)
        {
            // Load all actions with their subactions
            // Note: We need to load all because we can't efficiently filter by nested TimerGroupSetEnabledStateType in a query
            var allActions = await _context.Actions
                .Include(a => a.SubActions)
                .ToListAsync();

            bool hasChanges = false;
            foreach (var action in allActions)
            {
                var allTimerGroupSubActions = GetAllTimerGroupSubActions(action.SubActions);
                foreach (var timerGroupSubAction in allTimerGroupSubActions)
                {
                    if (timerGroupSubAction.TimerGroupId == timerGroupId && timerGroupSubAction.TimerGroupName != newName)
                    {
                        timerGroupSubAction.TimerGroupName = newName;
                        hasChanges = true;
                    }
                }
            }
            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates CommandName for all ToggleCommandDisabledType subactions (including nested) referencing the given command.
        /// </summary>
        public async Task UpdateToggleCommandDisabledNamesForRenamedCommand(string oldCommandName, string newCommandName)
        {
            // Load all actions with their subactions
            // Note: We need to load all because we can't efficiently filter by nested ToggleCommandDisabledType in a query
            var allActions = await _context.Actions
                .Include(a => a.SubActions)
                .ToListAsync();

            bool hasChanges = false;
            foreach (var action in allActions)
            {
                var allToggleCommandSubActions = GetAllToggleCommandDisabledSubActions(action.SubActions);
                foreach (var toggleCommandSubAction in allToggleCommandSubActions)
                {
                    if (string.Equals(toggleCommandSubAction.CommandName, oldCommandName, StringComparison.OrdinalIgnoreCase) && 
                        toggleCommandSubAction.CommandName != newCommandName)
                    {
                        toggleCommandSubAction.CommandName = newCommandName;
                        hasChanges = true;
                    }
                }
            }
            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Remaps Timer trigger IDs based on TimerGroupName after restore.
        /// Timer Group IDs change during restore, so we use names to re-establish the relationships.
        /// </summary>
        private async Task RemapTimerTriggerIds(DbContext context, List<ActionType> records, ILogger? logger)
        {
            var timerTriggers = records
                .SelectMany(a => a.Triggers)
                .Where(t => t.Type == TriggerTypes.Timer && !string.IsNullOrEmpty(t.Configuration))
                .ToList();

            if (!timerTriggers.Any())
            {
                logger?.LogDebug("No timer triggers to remap");
                return;
            }

            // Load all timer groups from the database (with their new IDs)
            var timerGroups = await context.Set<Bot.Models.Timers.TimerGroup>()
                .AsNoTracking()
                .ToListAsync();

            var timerGroupNameToIdMap = timerGroups
                .Where(tg => tg.Id.HasValue)
                .GroupBy(tg => tg.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => 
                    {
                        if (g.Count() > 1)
                        {
                            logger?.LogWarning("Multiple timer groups found with name '{Name}' (case-insensitive). Using first occurrence (ID: {Id})", g.Key, g.First().Id);
                        }
                        return g.First().Id!.Value;
                    },
                    StringComparer.OrdinalIgnoreCase);

            int remappedCount = 0;
            int failedCount = 0;

            foreach (var trigger in timerTriggers)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(trigger.Configuration);
                    if (config == null) continue;

                    // Try to get TimerGroupName (new format)
                    if (config.TryGetValue("TimerGroupName", out var timerGroupNameElement))
                    {
                        var timerGroupName = timerGroupNameElement.GetString();
                        if (!string.IsNullOrEmpty(timerGroupName) && 
                            timerGroupNameToIdMap.TryGetValue(timerGroupName, out var newTimerGroupId))
                        {
                            // Update the configuration with the new TimerGroupId
                            var newConfig = new
                            {
                                TimerGroupId = newTimerGroupId,
                                TimerGroupName = timerGroupName
                            };
                            trigger.Configuration = JsonSerializer.Serialize(newConfig);

                            // Update the reference column as well
                            trigger.TimerGroupId = newTimerGroupId;

                            // Update the trigger name to match the new ID
                            trigger.Name = $"TimerGroup_{newTimerGroupId}";

                            remappedCount++;
                            logger?.LogDebug("Remapped timer trigger for '{TimerGroupName}': old trigger name was {OldName}, new ID is {NewId}", 
                                timerGroupName, trigger.Name, newTimerGroupId);
                        }
                        else
                        {
                            failedCount++;
                            logger?.LogWarning("Timer trigger references unknown timer group: {TimerGroupName}", timerGroupName);
                        }
                    }
                    else
                    {
                        // Old format - only has TimerGroupId, cannot remap
                        failedCount++;
                        logger?.LogWarning("Timer trigger uses old format (TimerGroupId only) and cannot be remapped. Configuration: {Config}", trigger.Configuration);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    logger?.LogError(ex, "Failed to remap timer trigger. Configuration: {Config}", trigger.Configuration);
                }
            }

            if (remappedCount > 0 || failedCount > 0)
            {
                await context.SaveChangesAsync();
                logger?.LogInformation("Remapped {RemappedCount} timer triggers, {FailedCount} failed", remappedCount, failedCount);
            }
        }

        /// <summary>
        /// Remaps Command trigger IDs based on CommandName after restore.
        /// Command IDs change during restore, so we use names to re-establish the relationships.
        /// </summary>
        private async Task RemapCommandTriggerIds(DbContext context, List<ActionType> records, ILogger? logger)
        {
            var commandTriggers = records
                .SelectMany(a => a.Triggers)
                .Where(t => t.Type == TriggerTypes.Command && !string.IsNullOrEmpty(t.Configuration))
                .ToList();

            if (!commandTriggers.Any())
            {
                logger?.LogDebug("No command triggers to remap");
                return;
            }

            // Load all action commands from the database (with their new IDs)
            var actionCommands = await context.Set<Bot.Models.Commands.ActionCommand>()
                .AsNoTracking()
                .ToListAsync();

            var commandNameToIdMap = actionCommands
                .Where(ac => ac.Id.HasValue)
                .GroupBy(ac => ac.CommandName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => 
                    {
                        if (g.Count() > 1)
                        {
                            logger?.LogWarning("Multiple commands found with name '{Name}' (case-insensitive). Using first occurrence (ID: {Id})", g.Key, g.First().Id);
                        }
                        return g.First().Id!.Value;
                    },
                    StringComparer.OrdinalIgnoreCase);

            int remappedCount = 0;
            int failedCount = 0;

            foreach (var trigger in commandTriggers)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(trigger.Configuration);
                    if (config == null) continue;

                    // Try to get CommandName
                    if (config.TryGetValue("CommandName", out var commandNameElement))
                    {
                        var commandName = commandNameElement.GetString();
                        if (!string.IsNullOrEmpty(commandName) && 
                            commandNameToIdMap.TryGetValue(commandName, out var newCommandId))
                        {
                            // Update the configuration with the new CommandId
                            var newConfig = new
                            {
                                CommandId = newCommandId,
                                CommandName = commandName
                            };
                            trigger.Configuration = JsonSerializer.Serialize(newConfig);

                            // Update the reference column as well
                            trigger.CommandId = newCommandId;

                            remappedCount++;
                            logger?.LogDebug("Remapped command trigger for '!{CommandName}': new ID is {NewId}", commandName, newCommandId);
                        }
                        else
                        {
                            failedCount++;
                            logger?.LogWarning("Command trigger references unknown command: {CommandName}", commandName);
                        }
                    }
                    else
                    {
                        // Old format - only has CommandId, cannot remap
                        failedCount++;
                        logger?.LogWarning("Command trigger uses old format (CommandId only) and cannot be remapped. Configuration: {Config}", trigger.Configuration);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    logger?.LogError(ex, "Failed to remap command trigger. Configuration: {Config}", trigger.Configuration);
                }
            }

            if (remappedCount > 0 || failedCount > 0)
            {
                await context.SaveChangesAsync();
                logger?.LogInformation("Remapped {RemappedCount} command triggers, {FailedCount} failed", remappedCount, failedCount);
            }
        }

        /// <summary>
        /// Remaps TimerGroupSetEnabledState SubAction IDs based on TimerGroupName after restore.
        /// Timer Group IDs change during restore, so we use names to re-establish the relationships.
        /// </summary>
        private async Task RemapTimerGroupSubActionIds(DbContext context, List<ActionType> records, ILogger? logger)
        {
            var timerGroupSubActions = records
                .SelectMany(a => GetAllTimerGroupSubActions(a.SubActions))
                .ToList();

            if (!timerGroupSubActions.Any())
            {
                logger?.LogDebug("No TimerGroupSetEnabledState subactions to remap");
                return;
            }

            // Load all timer groups from the database (with their new IDs)
            var timerGroups = await context.Set<Bot.Models.Timers.TimerGroup>()
                .AsNoTracking()
                .ToListAsync();

            var timerGroupNameToIdMap = timerGroups
                .Where(tg => tg.Id.HasValue)
                .GroupBy(tg => tg.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => 
                    {
                        if (g.Count() > 1)
                        {
                            logger?.LogWarning("Multiple timer groups found with name '{Name}' (case-insensitive). Using first occurrence (ID: {Id})", g.Key, g.First().Id);
                        }
                        return g.First().Id!.Value;
                    },
                    StringComparer.OrdinalIgnoreCase);

            int remappedCount = 0;
            int failedCount = 0;

            foreach (var subAction in timerGroupSubActions)
            {
                try
                {
                    // Check if we have a TimerGroupName to remap with
                    if (!string.IsNullOrEmpty(subAction.TimerGroupName))
                    {
                        if (timerGroupNameToIdMap.TryGetValue(subAction.TimerGroupName, out var newTimerGroupId))
                        {
                            subAction.TimerGroupId = newTimerGroupId;
                            remappedCount++;
                            logger?.LogDebug("Remapped TimerGroupSetEnabledState subaction for '{TimerGroupName}': new ID is {NewId}", 
                                subAction.TimerGroupName, newTimerGroupId);
                        }
                        else
                        {
                            failedCount++;
                            logger?.LogWarning("TimerGroupSetEnabledState subaction references unknown timer group: {TimerGroupName}", 
                                subAction.TimerGroupName);
                        }
                    }
                    else
                    {
                        // Old format - only has TimerGroupId, cannot remap
                        failedCount++;
                        logger?.LogWarning("TimerGroupSetEnabledState subaction uses old format (TimerGroupId only) and cannot be remapped. TimerGroupId: {TimerGroupId}", 
                            subAction.TimerGroupId);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    logger?.LogError(ex, "Failed to remap TimerGroupSetEnabledState subaction. TimerGroupId: {TimerGroupId}, TimerGroupName: {TimerGroupName}", 
                        subAction.TimerGroupId, subAction.TimerGroupName);
                }
            }

            if (remappedCount > 0 || failedCount > 0)
            {
                await context.SaveChangesAsync();
                logger?.LogInformation("Remapped {RemappedCount} TimerGroupSetEnabledState subactions, {FailedCount} failed", 
                    remappedCount, failedCount);
            }
        }

        /// <summary>
        /// Recursively finds all TimerGroupSetEnabledStateType subactions, including those nested in LogicIfElseType.
        /// </summary>
        private List<TimerGroupSetEnabledStateType> GetAllTimerGroupSubActions(IEnumerable<SubActionType> subActions)
        {
            var result = new List<TimerGroupSetEnabledStateType>();
            foreach (var subAction in subActions)
            {
                if (subAction is TimerGroupSetEnabledStateType timerGroup)
                {
                    result.Add(timerGroup);
                }
                else if (subAction is LogicIfElseType logic)
                {
                    result.AddRange(GetAllTimerGroupSubActions(logic.TrueSubActions));
                    result.AddRange(GetAllTimerGroupSubActions(logic.FalseSubActions));
                }
            }
            return result;
        }

        /// <summary>
        /// Remaps ToggleCommandDisabled SubAction CommandNames after restore.
        /// This ensures that command references are maintained even if command IDs change.
        /// Note: CommandName should already be populated during restore, but this validates and fixes any issues.
        /// </summary>
        private async Task RemapToggleCommandDisabledSubActions(DbContext context, List<ActionType> records, ILogger? logger)
        {
            var toggleCommandSubActions = records
                .SelectMany(a => GetAllToggleCommandDisabledSubActions(a.SubActions))
                .ToList();

            if (!toggleCommandSubActions.Any())
            {
                logger?.LogDebug("No ToggleCommandDisabled subactions to remap");
                return;
            }

            // Load all action commands from the database
            var actionCommands = await context.Set<Bot.Models.Commands.ActionCommand>()
                .AsNoTracking()
                .ToListAsync();

            var commandNameSet = new HashSet<string>(
                actionCommands.Select(ac => ac.CommandName),
                StringComparer.OrdinalIgnoreCase);

            int validatedCount = 0;
            int failedCount = 0;

            foreach (var subAction in toggleCommandSubActions)
            {
                try
                {
                    // Check if CommandName exists in the current database
                    if (!string.IsNullOrEmpty(subAction.CommandName))
                    {
                        if (commandNameSet.Contains(subAction.CommandName))
                        {
                            validatedCount++;
                            logger?.LogDebug("Validated ToggleCommandDisabled subaction for command '{CommandName}'", subAction.CommandName);
                        }
                        else
                        {
                            failedCount++;
                            logger?.LogWarning("ToggleCommandDisabled subaction references unknown command: {CommandName}", subAction.CommandName);
                        }
                    }
                    else
                    {
                        // Empty CommandName
                        failedCount++;
                        logger?.LogWarning("ToggleCommandDisabled subaction has empty CommandName");
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    logger?.LogError(ex, "Failed to validate ToggleCommandDisabled subaction. CommandName: {CommandName}", 
                        subAction.CommandName);
                }
            }

            logger?.LogInformation("Validated {ValidatedCount} ToggleCommandDisabled subactions, {FailedCount} failed", 
                validatedCount, failedCount);
        }

        /// <summary>
        /// Recursively finds all ToggleCommandDisabledType subactions, including those nested in LogicIfElseType.
        /// </summary>
        private List<ToggleCommandDisabledType> GetAllToggleCommandDisabledSubActions(IEnumerable<SubActionType> subActions)
        {
            var result = new List<ToggleCommandDisabledType>();
            foreach (var subAction in subActions)
            {
                if (subAction is ToggleCommandDisabledType toggleCommand)
                {
                    result.Add(toggleCommand);
                }
                else if (subAction is LogicIfElseType logic)
                {
                    result.AddRange(GetAllToggleCommandDisabledSubActions(logic.TrueSubActions));
                    result.AddRange(GetAllToggleCommandDisabledSubActions(logic.FalseSubActions));
                }
            }
            return result;
        }
    }
}

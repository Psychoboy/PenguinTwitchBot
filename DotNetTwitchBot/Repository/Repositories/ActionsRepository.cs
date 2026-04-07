using DotNetTwitchBot.Bot.Actions;
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
            await PopulateExecuteActionNamesBeforeSave(action.SubActions);

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
                if (actionIdToNameMap.TryGetValue(executeAction.ActionId, out var actionName))
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
                    if (actionIdToNameMap.TryGetValue(subAction.ActionId, out var actionName))
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

                // Delete Actions (cascade will handle SubActions and Triggers)
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
    }
}

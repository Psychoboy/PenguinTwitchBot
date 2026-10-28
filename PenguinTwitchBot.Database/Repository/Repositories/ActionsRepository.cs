using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ActionsRepository : GenericRepository<ActionType>, IActionsRepository
    {
        public ActionsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ActionType?> GetByIdWithDetailsAsync(Guid id)
        {
            var action = await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.CatchSubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (action != null)
            {
                MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.SubActions);
                MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.CatchSubActions);
            }

            return action;
        }

        public async Task<List<ActionType>> GetAllWithDetailsAsync()
        {
            var actions = await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.CatchSubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .OrderBy(a => a.Name)
                .ToListAsync();

            foreach (var action in actions)
            {
                MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.SubActions);
                MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.CatchSubActions);
            }

            return actions;
        }

        public async Task<List<ActionType>> GetAllSummariesAsync()
        {
            return await _context.Actions
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<ActionType> CreateActionAsync(ActionType action)
        {
            if (action.Id == Guid.Empty)
            {
                action.Id = Guid.CreateVersion7();
            }

            action.SubActions ??= [];
            action.CatchSubActions ??= [];

            MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.SubActions);
            MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.CatchSubActions);

            var allIncomingSubActions = GetAllTopLevelSubActions(action);

            // Assign IDs to new SubActions
            foreach (var subAction in allIncomingSubActions.Where(s => s.Id == Guid.Empty))
            {
                subAction.Id = Guid.CreateVersion7();
            }

            await _context.Actions.AddAsync(action);
            await _context.SaveChangesAsync();

            return action;
        }

        public async Task<ActionType> UpdateActionAsync(ActionType action)
        {
            if (action.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Cannot update action: Action ID is empty");
            }

            // Load the existing action from database WITH tracking
            var existingAction = await _context.Actions
                .Include(a => a.SubActions)
                .Include(a => a.CatchSubActions)
                .Include(a => a.Triggers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == action.Id);

            if (existingAction == null)
            {
                throw new InvalidOperationException($"Action with ID {action.Id} not found");
            }

            // Update scalar properties on the tracked entity
            _context.Entry(existingAction).CurrentValues.SetValues(action);

            action.SubActions ??= [];
            action.CatchSubActions ??= [];

            MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.SubActions);
            MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(action.CatchSubActions);

            // Handle SubActions: Remove deleted, Add new, Update existing
            SyncSubActionCollection(existingAction.Id, isCatch: false, existingAction.SubActions, action.SubActions);
            SyncSubActionCollection(existingAction.Id, isCatch: true, existingAction.CatchSubActions, action.CatchSubActions);

            // Handle Triggers: Remove deleted, Add new, Update existing
            var existingTriggerIds = existingAction.Triggers.Select(t => t.Id).ToHashSet();
            var newTriggerIds = action.Triggers.Where(t => t.Id != Guid.Empty).Select(t => t.Id).ToHashSet();

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
                    trigger.ActionId = action.Id;
                    existingAction.Triggers.Add(trigger);
                }
            }

            await _context.SaveChangesAsync();

            return existingAction;
        }

        public async Task DeleteActionAsync(Guid id)
        {
            var action = await _context.Actions.FindAsync(id);
            if (action != null)
            {
                _context.Actions.Remove(action);
                await _context.SaveChangesAsync();
            }
        }

        private static List<SubActionType> GetAllTopLevelSubActions(ActionType action)
        {
            return [.. action.SubActions, .. action.CatchSubActions];
        }

        private void SyncSubActionCollection(Guid actionId, bool isCatch, List<SubActionType> existingSubActions, List<SubActionType> incomingSubActions)
        {
            var existingSubActionIds = existingSubActions.Select(s => s.Id).ToHashSet();
            var incomingSubActionIds = incomingSubActions.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();

            var subActionsToRemove = existingSubActions
                .Where(s => !incomingSubActionIds.Contains(s.Id))
                .ToList();

            foreach (var subAction in subActionsToRemove)
            {
                existingSubActions.Remove(subAction);
                _context.Entry(subAction).State = EntityState.Deleted;
            }

            foreach (var subAction in incomingSubActions)
            {
                if (isCatch)
                {
                    subAction.CatchActionTypeId = actionId;
                    subAction.ActionTypeId = null;
                }
                else
                {
                    subAction.ActionTypeId = actionId;
                    subAction.CatchActionTypeId = null;
                }

                if (subAction.Id == Guid.Empty || !existingSubActionIds.Contains(subAction.Id))
                {
                    if (subAction.Id == Guid.Empty)
                    {
                        subAction.Id = Guid.CreateVersion7();
                    }
                    existingSubActions.Add(subAction);
                    _context.Entry(subAction).State = EntityState.Added;
                }
                else
                {
                    var existingSubAction = existingSubActions.FirstOrDefault(s => s.Id == subAction.Id);
                    if (existingSubAction != null && existingSubAction.GetType() != subAction.GetType())
                    {
                        existingSubActions.Remove(existingSubAction);
                        _context.Entry(existingSubAction).State = EntityState.Deleted;
                        subAction.Id = Guid.CreateVersion7();
                        existingSubActions.Add(subAction);
                        _context.Entry(subAction).State = EntityState.Added;
                    }
                    else if (existingSubAction != null)
                    {
                        if (isCatch)
                        {
                            existingSubAction.CatchActionTypeId = actionId;
                            existingSubAction.ActionTypeId = null;
                        }
                        else
                        {
                            existingSubAction.ActionTypeId = actionId;
                            existingSubAction.CatchActionTypeId = null;
                        }

                        _context.Entry(existingSubAction).CurrentValues.SetValues(subAction);

                        var existingActionEntry = _context.Entry(existingSubAction);
                        if (existingActionEntry.State == EntityState.Detached)
                        {
                            existingSubActions.Remove(existingSubAction);
                            existingSubActions.Add(subAction);
                        }
                    }
                }
            }
        }

        public async Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName)
        {
            return await _context.Actions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.SubActions)
                .Include(a => a.CatchSubActions)
                .Include(a => a.Triggers)
                .Where(a => a.Triggers.Any(t => 
                    t.Type == triggerType && 
                    t.Name == triggerName &&
                    t.Enabled))
                .OrderBy(a => a.Name)
                .ToListAsync();
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
                .Include(a => a.CatchSubActions)
                .AsSplitQuery()
                .ToListAsync();

            bool hasChanges = false;
            foreach (var action in allActions)
            {
                var allToggleCommandSubActions = GetAllToggleCommandDisabledSubActions(action.SubActions.Concat(action.CatchSubActions));
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

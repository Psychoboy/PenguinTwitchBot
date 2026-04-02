using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using Microsoft.EntityFrameworkCore;

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
                .Include(a => a.ActionTriggers)
                    .ThenInclude(at => at.Trigger)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<ActionType>> GetAllWithDetailsAsync()
        {
            return await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.ActionTriggers)
                    .ThenInclude(at => at.Trigger)
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
                .Include(a => a.ActionTriggers)
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
            foreach (var subAction in action.SubActions)
            {
                // Check if this is a new SubAction (Id = 0 or not in existing)
                if (subAction.Id == 0 || !existingSubActionIds.Contains(subAction.Id))
                {
                    // New SubAction - generate ID
                    if (subAction.Id == 0)
                    {
                        // Get the next available ID
                        var maxId = await _context.SubActions.MaxAsync(s => (int?)s.Id) ?? 0;
                        subAction.Id = maxId + 1;
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

            // Handle ActionTriggers - update only Enabled and Priority
            foreach (var actionTrigger in action.ActionTriggers)
            {
                var existing = existingAction.ActionTriggers
                    .FirstOrDefault(at => at.Id == actionTrigger.Id);

                if (existing != null)
                {
                    existing.Enabled = actionTrigger.Enabled;
                    existing.Priority = actionTrigger.Priority;
                }
            }

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
    }
}

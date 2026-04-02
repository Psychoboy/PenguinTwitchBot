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
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<ActionType>> GetAllWithDetailsAsync()
        {
            return await _context.Actions
                .AsNoTracking()
                .Include(a => a.SubActions)
                .Include(a => a.ActionTriggers)
                    .ThenInclude(at => at.Trigger)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<ActionType> CreateActionAsync(ActionType action)
        {
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
                .FirstOrDefaultAsync(a => a.Id == action.Id.Value);

            if (existingAction == null)
            {
                throw new InvalidOperationException($"Action with ID {action.Id.Value} not found");
            }

            // Update scalar properties on the tracked entity
            _context.Entry(existingAction).CurrentValues.SetValues(action);

            // Handle SubActions: Remove deleted, Add new, Update existing
            var existingSubActionIds = existingAction.SubActions.Select(s => s.Id).ToHashSet();
            var newSubActionIds = action.SubActions.Select(s => s.Id).ToHashSet();

            // Remove SubActions that are no longer in the incoming list
            var subActionsToRemove = existingAction.SubActions
                .Where(s => !newSubActionIds.Contains(s.Id))
                .ToList();
            foreach (var subAction in subActionsToRemove)
            {
                existingAction.SubActions.Remove(subAction);
            }

            // Process incoming SubActions
            foreach (var subAction in action.SubActions)
            {
                // Check if this is a new SubAction (Guid.Empty or not in existing)
                if (subAction.Id == Guid.Empty || !existingSubActionIds.Contains(subAction.Id))
                {
                    // New SubAction - generate ID if needed
                    if (subAction.Id == Guid.Empty)
                    {
                        subAction.Id = Guid.NewGuid();
                    }

                    // Add to collection - EF will INSERT this
                    existingAction.SubActions.Add(subAction);
                }
                else
                {
                    // Existing SubAction - update its properties
                    var existingSubAction = existingAction.SubActions.First(s => s.Id == subAction.Id);
                    _context.Entry(existingSubAction).CurrentValues.SetValues(subAction);
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

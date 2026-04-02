using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
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

                // Add the Actions with all their related entities
                foreach (var record in records)
                {
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
    }
}

using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions
{
    public class ActionManagementService : IActionManagementService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ActionManagementService> _logger;

        public ActionManagementService(IServiceScopeFactory serviceScopeFactory, ILogger<ActionManagementService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<List<ActionType>> GetAllActionsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var actions = await unitOfWork.Actions.GetAllWithDetailsAsync();

            // Ensure SubActions are ordered by Index
            foreach (var action in actions)
            {
                action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
                action.CatchSubActions = action.CatchSubActions.OrderBy(s => s.Index).ToList();
            }

            return actions;
        }

        public async Task<List<ActionType>> GetActionSummariesAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.Actions.GetAllSummariesAsync();
        }

        public async Task<ActionType?> GetActionByIdAsync(Guid id)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var action = await unitOfWork.Actions.GetByIdWithDetailsAsync(id);

            // Ensure SubActions are ordered by Index
            if (action != null)
            {
                action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
                action.CatchSubActions = action.CatchSubActions.OrderBy(s => s.Index).ToList();
            }

            return action;
        }

        public async Task<ActionType> CreateActionAsync(ActionType action)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            
            try
            {
                return await unitOfWork.Actions.CreateActionAsync(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating action {ActionName}", action.Name);
                throw;
            }
        }

        public async Task<ActionType> UpdateActionAsync(ActionType action)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                return await unitOfWork.Actions.UpdateActionAsync(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating action {ActionName}", action.Name);
                throw;
            }
        }

        public async Task DeleteActionAsync(Guid id)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                await unitOfWork.Actions.DeleteActionAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting action {ActionId}", id);
                throw;
            }
        }

        public async Task<List<ActionType>> GetActionsByTriggerTypeAndNameAsync(TriggerTypes triggerType, string triggerName)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                var actions = await unitOfWork.Actions.GetActionsByTriggerTypeAndNameAsync(triggerType, triggerName);

                // Ensure SubActions are ordered by Index
                foreach (var action in actions)
                {
                    action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
                    action.CatchSubActions = action.CatchSubActions.OrderBy(s => s.Index).ToList();
                }

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting actions by trigger type {TriggerType} and name {TriggerName}", triggerType, triggerName);
                throw;
            }
        }

        public async Task<List<ActionType>> GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes triggerType, string triggerName)
        {
            var actions = await GetActionsByTriggerTypeAndNameAsync(triggerType, triggerName);
            return actions.Where(a => a.Enabled).ToList();
        }

        public async Task<List<ActionType>> GetActionsByCommandIdEnabledAsync(Guid commandId, string legacyTriggerName) =>
            (await GetActionsAsync(
                repository => repository.GetActionsByCommandIdAsync(commandId, legacyTriggerName),
                $"command {commandId}")).Where(a => a.Enabled).ToList();

        public async Task<List<ActionType>> GetActionsByKeywordIdEnabledAsync(Guid keywordId, string legacyTriggerName) =>
            (await GetActionsByKeywordIdAsync(keywordId, legacyTriggerName)).Where(a => a.Enabled).ToList();

        public Task<List<ActionType>> GetActionsByKeywordIdAsync(Guid keywordId, string legacyTriggerName) =>
            GetActionsAsync(
                repository => repository.GetActionsByKeywordIdAsync(keywordId, legacyTriggerName),
                $"keyword {keywordId}");

        private async Task<List<ActionType>> GetActionsAsync(
            Func<IActionsRepository, Task<List<ActionType>>> query,
            string description)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                var actions = await query(unitOfWork.Actions);

                // Ensure SubActions are ordered by Index
                foreach (var action in actions)
                {
                    action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
                    action.CatchSubActions = action.CatchSubActions.OrderBy(s => s.Index).ToList();
                }

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting actions for {Description}", description);
                throw;
            }
        }

        public async Task RenameTriggersAsync(TriggerTypes triggerType, Guid referenceId, string newName)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.Triggers.RenameByReferenceAsync(triggerType, referenceId, newName);
        }

        public async Task<List<TriggerType>> GetTriggersForActionAsync(Guid actionId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.Triggers.GetTriggersForActionAsync(actionId);
        }

        public async Task<List<TriggerType>> GetAllTriggersAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.Triggers.GetAllAsync();
        }

        public async Task<TriggerType?> GetTriggerByIdAsync(Guid id)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.Triggers.GetByIdAsync(id);
        }

        public async Task<TriggerType> CreateTriggerAsync(TriggerType trigger)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                return await unitOfWork.Triggers.AddAsync(trigger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating trigger {TriggerName}", trigger.Name);
                throw;
            }
        }

        public async Task<TriggerType> UpdateTriggerAsync(TriggerType trigger)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                return await unitOfWork.Triggers.UpdateAsync(trigger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trigger {TriggerName}", trigger.Name);
                throw;
            }
        }

        public async Task DeleteTriggerAsync(Guid triggerId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                await unitOfWork.Triggers.DeleteAsync(triggerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trigger {TriggerId}", triggerId);
                throw;
            }
        }

        public async Task DeleteTriggersForCommandAsync(Guid commandId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                var triggers = await unitOfWork.Triggers.GetByCommandIdAsync(commandId);
                _logger.LogInformation("Deleting {Count} triggers for command ID {CommandId}", triggers.Count, commandId);

                foreach (var trigger in triggers)
                {
                    await unitOfWork.Triggers.DeleteAsync(trigger.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting triggers for command {CommandId}", commandId);
                throw;
            }
        }

        public async Task DeleteTriggersForKeywordAsync(Guid keywordId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                var triggers = await unitOfWork.Triggers.GetByKeywordIdAsync(keywordId);
                _logger.LogInformation("Deleting {Count} triggers for keyword ID {KeywordId}", triggers.Count, keywordId);

                foreach (var trigger in triggers)
                {
                    await unitOfWork.Triggers.DeleteAsync(trigger.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting triggers for keyword {KeywordId}", keywordId);
                throw;
            }
        }
    }
}

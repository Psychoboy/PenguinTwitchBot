using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetTwitchBot.Bot.Actions
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
            }

            return actions;
        }

        public async Task<ActionType?> GetActionByIdAsync(int id)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var action = await unitOfWork.Actions.GetByIdWithDetailsAsync(id);

            // Ensure SubActions are ordered by Index
            if (action != null)
            {
                action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
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

        public async Task DeleteActionAsync(int id)
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
            var actions = await unitOfWork.Actions.GetActionsByTriggerTypeAndNameAsync(triggerType, triggerName);

            // Ensure SubActions are ordered by Index
            foreach (var action in actions)
            {
                action.SubActions = action.SubActions.OrderBy(s => s.Index).ToList();
            }

            return actions;
        }

        public async Task<List<ActionTrigger>> GetTriggersForActionAsync(int actionId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.ActionTriggers.GetByActionIdAsync(actionId);
        }

        public async Task<ActionTrigger> AddTriggerToActionAsync(ActionTrigger actionTrigger)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            
            try
            {
                return await unitOfWork.ActionTriggers.AddAsync(actionTrigger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding trigger to action {ActionId}", actionTrigger.ActionId);
                throw;
            }
        }

        public async Task RemoveTriggerFromActionAsync(int actionTriggerId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                await unitOfWork.ActionTriggers.DeleteAsync(actionTriggerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing trigger {ActionTriggerId}", actionTriggerId);
                throw;
            }
        }

        public async Task<List<TriggerType>> GetAllTriggersAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.Triggers.GetAllAsync();
        }

        public async Task<TriggerType?> GetTriggerByIdAsync(int id)
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
    }
}

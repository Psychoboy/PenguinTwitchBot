using MudBlazor;
using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Database.Bot.Models.Queues;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private int GetTimedAnnouncementNumber(string key, int fallback) =>
        int.TryParse(GetParameter(key), out var value) ? value : fallback;

    private Task OnTimedAnnouncementNumberChanged(string key, int value)
    {
        SetParameter(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    private long GetTimedRaffleAward() =>
        long.TryParse(GetParameter("TotalAward"), out var award) ? award : 0;

    private Task OnTimedRaffleAwardChanged(long award)
    {
        SetParameter("TotalAward", award.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    private async Task CreateTimerGroupActionsAsync(ITimerGroupActionRecipe recipe, string actionName, string queueName)
    {
        var timerGroup = recipe.BuildTimerGroup(_parameters);
        var existingTimerGroups = await TimerService.GetTimerGroupsAsync();
        if (existingTimerGroups.Any(group => group.Name.Equals(timerGroup.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _error = $"A timer group named '{timerGroup.Name}' already exists.";
            return;
        }

        var actions = recipe.BuildTimerActions(
            new ActionRecipeContext(actionName, queueName, string.Empty, Guid.Empty),
            timerGroup,
            _parameters);
        var existingActions = await ActionService.GetActionSummariesAsync();
        var duplicateName = actions.FirstOrDefault(action => existingActions.Any(existing =>
            existing.Name.Equals(action.Name, StringComparison.OrdinalIgnoreCase)));
        if (duplicateName is not null)
        {
            _error = $"An action named '{duplicateName.Name}' already exists.";
            return;
        }

        if (_pendingQueue is { } pendingQueue)
        {
            if (queueName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                _queues.Any(queue => queue.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase)))
            {
                _error = "Choose an available queue name for the new queue.";
                return;
            }

            var createdQueue = await QueueManager.CreateQueueAsync(pendingQueue);
            _queues.Add(createdQueue);
            _selectedQueueName = createdQueue.Name;
            _pendingQueue = null;
        }

        await TimerService.AddTimerGroup(timerGroup);
        var createdActions = new List<Guid>();
        try
        {
            foreach (var action in actions)
            {
                action.Group = EffectiveGroupName;
                var createdAction = await ActionService.CreateActionAsync(action);
                createdActions.Add(createdAction.Id);
                try
                {
                    await RecipeTagStore.TagAsync(createdAction.Id, recipe.Id, _parameters);
                }
                catch (Exception tagException)
                {
                    Snackbar.Add($"Action '{createdAction.Name}' was created, but its recipe tag could not be saved: {tagException.Message}", Severity.Warning);
                }
            }
        }
        catch
        {
            foreach (var actionId in createdActions)
                await ActionService.DeleteActionAsync(actionId);
            await TimerService.DeleteTimerGroup(timerGroup);
            throw;
        }

        Snackbar.Add($"Created timer group '{timerGroup.Name}' with {createdActions.Count} announcement actions.", Severity.Success);
        MudDialog.Close(DialogResult.Ok(createdActions[0]));
    }

    private async Task CreateRaffleSetupAsync(IRaffleSetupActionRecipe recipe, string queueName)
    {
        var request = recipe.BuildRequest(EffectiveGroupName, queueName, _parameters);
        var createdQueue = false;
        if (_pendingQueue is { } pendingQueue)
        {
            if (queueName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                _queues.Any(queue => queue.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase)))
            {
                _error = "Choose an available queue name for the new raffle queue.";
                return;
            }

            var queue = await QueueManager.CreateQueueAsync(pendingQueue);
            _queues.Add(queue);
            _selectedQueueName = queue.Name;
            _pendingQueue = null;
            createdQueue = true;
        }

        try
        {
            var result = await RaffleSetupService.CreateAsync(request);
            if (!result.Success)
            {
                _error = string.Join(" ", result.Errors);
                await RollBackPendingRaffleQueueAsync(createdQueue, queueName);
                return;
            }

            foreach (var actionId in new[] { result.JoinActionId, result.StartActionId, result.EndActionId }.Where(id => id.HasValue).Select(id => id!.Value))
            {
                try
                {
                    await RecipeTagStore.TagAsync(actionId, recipe.Id, _parameters);
                }
                catch (Exception tagException)
                {
                    Snackbar.Add($"Raffle created, but a recipe tag could not be saved: {tagException.Message}", Severity.Warning);
                }
            }

            Snackbar.Add($"Created timed raffle '{result.RaffleKey}' with join command !{result.JoinCommandName} and start command !{result.StartCommandName}.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(result.StartActionId));
        }
        catch
        {
            await RollBackPendingRaffleQueueAsync(createdQueue, queueName);
            throw;
        }
    }

    private async Task RollBackPendingRaffleQueueAsync(bool createdQueue, string queueName)
    {
        if (!createdQueue)
            return;
        await QueueManager.DeleteQueueAsync(queueName);
        _queues.RemoveAll(queue => queue.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));
        _selectedQueueName = "Default";
        _pendingQueue = new QueueConfiguration
        {
            Name = queueName,
            IsBlocking = true,
            Enabled = true,
            MaxConcurrentActions = 1
        };
    }
}
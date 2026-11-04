using MudBlazor;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Bot.Models.Queues;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (SelectedRecipe == null)
        {
            error = "Choose a recipe.";
            return false;
        }
        SyncAlertRecipeParameters();
        if (SelectedRecipe is AlertRecipe &&
            (string.IsNullOrWhiteSpace(_alertRecipeSettings.Text) ||
             _alertRecipeSettings.Duration is < 1 or > 60 ||
             _alertRecipeSettings.Volume is < 0 or > 1))
        {
            error = "Enter alert text, a duration from 1 to 60 seconds, and a volume from 0 to 100%.";
            return false;
        }
        if (SelectedRecipe is TwitchResponseRecipeBase && IsTwitchAlertEnabled &&
            (string.IsNullOrWhiteSpace(_alertRecipeSettings.Text) ||
             _alertRecipeSettings.Duration is < 1 or > 60 ||
             _alertRecipeSettings.Volume is < 0 or > 1 ||
             !int.TryParse(GetParameter("FireworksCount"), out var fireworksCount) || fireworksCount is < 0 or > 100))
        {
            error = "Enter alert text, a valid alert duration and volume, and fireworks bursts from 0 to 100.";
            return false;
        }
        if (SelectedRecipe is TwitchResponseRecipeBase &&
            (!int.TryParse(GetParameter("FireworksCount"), out var reactionFireworksCount) || reactionFireworksCount is < 0 or > 100))
        {
            error = "Fireworks bursts must be between 0 and 100.";
            return false;
        }
        if (SelectedRecipe is PointsRewardRecipe &&
            (!_pointTypeNames.Contains(GetParameter("PointTypeName"), StringComparer.OrdinalIgnoreCase) ||
             !Enum.TryParse<ManagePointsOperation>(GetParameter("Operation"), true, out _) ||
             string.IsNullOrWhiteSpace(GetParameter("Amount")) ||
             (!GetParameter("Operation").Equals(nameof(ManagePointsOperation.AddActive), StringComparison.OrdinalIgnoreCase) &&
              string.IsNullOrWhiteSpace(GetParameter("TargetUser")))))
        {
            error = "Choose an existing point type, a valid operation, and its required amount and target user.";
            return false;
        }
        if (SelectedRecipe is ChannelPointActionRecipe channelPointRecipe)
        {
            if (IsCreatingChannelPointReward)
            {
                try
                {
                    var request = channelPointRecipe.BuildRewardRequest(_parameters);
                    if (_channelPointRewardTitles.Contains(request.Title, StringComparer.OrdinalIgnoreCase))
                    {
                        error = "A reward with that title already exists. Select it from the existing reward list instead.";
                        return false;
                    }
                }
                catch (ArgumentException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
            else if (!_channelPointRewardTitles.Contains(GetParameter("RewardTitle"), StringComparer.OrdinalIgnoreCase))
            {
                error = "Choose an existing reward or switch to Create new reward.";
                return false;
            }
        }
        if (SelectedRecipe is CommandAliasRecipe &&
            !_enabledCommandNames.Contains(GetParameter("TargetCommand"), StringComparer.OrdinalIgnoreCase))
        {
            error = "Choose an existing enabled command to alias.";
            return false;
        }
        if (SelectedRecipe is CommandGroupToggleRecipe &&
            (GetSelectedToggleCommands().Count == 0 ||
             GetSelectedToggleCommands().Any(commandName => !_commandNames.Contains(commandName, StringComparer.OrdinalIgnoreCase)) ||
             (!GetParameter("Operation").Equals("Enable", StringComparison.OrdinalIgnoreCase) &&
              !GetParameter("Operation").Equals("Disable", StringComparison.OrdinalIgnoreCase))))
        {
            error = "Choose at least one existing command and select Enable or Disable.";
            return false;
        }
        if (SelectedRecipe is CounterCommandRecipe &&
            (!_counterNames.Contains(GetParameter("CounterName"), StringComparer.OrdinalIgnoreCase) ||
             !int.TryParse(GetParameter("Step"), out var counterStep) || counterStep <= 0))
        {
            error = "Choose an existing counter and enter a positive increment amount.";
            return false;
        }
        if (SelectedRecipe is TimedAnnouncementRecipe &&
            (string.IsNullOrWhiteSpace(GetParameter("TimerName")) ||
             !int.TryParse(GetParameter("IntervalSeconds"), out var intervalSeconds) || intervalSeconds is < 30 or > 86400 ||
             !int.TryParse(GetParameter("MinimumMessages"), out var minimumMessages) || minimumMessages is < 0 or > 10000 ||
             !Enumerable.Range(1, 5).Any(index => !string.IsNullOrWhiteSpace(GetParameter($"Message{index}")))))
        {
            error = "Enter a unique timer name, interval from 30 to 86400 seconds, a valid chat activity threshold, and at least one message.";
            return false;
        }
        if (SelectedRecipe is TimedRaffleRecipe raffleRecipe)
        {
            try
            {
                raffleRecipe.BuildRequest(EffectiveGroupName, EffectiveQueueName, _parameters);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }
        }
        if (SelectedRecipe is ObsSourceVisibilityRecipe &&
            (!Guid.TryParse(GetParameter("OBSConnectionId"), out var connectionId) || connectionId == Guid.Empty ||
             string.IsNullOrWhiteSpace(GetParameter("SceneName")) ||
             string.IsNullOrWhiteSpace(GetParameter("SourceName")) ||
             !int.TryParse(GetParameter("DurationSeconds"), out var durationSeconds) || durationSeconds is < 1 or > 3600))
        {
            error = "Choose an OBS connection, scene, source, and a visibility duration from 1 to 3600 seconds.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(_actionName))
        {
            error = "Action name is required.";
            return false;
        }
        if (SelectedRecipe.RequiresCommand &&
            (string.IsNullOrWhiteSpace(NormalizedCommandName) || NormalizedCommandName.Any(char.IsWhiteSpace)))
        {
            error = "Enter a single command name without spaces.";
            return false;
        }
        if (SelectedRecipe.RequiresTrigger && !_additionalTriggers.Any(SelectedRecipe.IsRequiredTrigger))
        {
            error = $"Add a supported trigger for {SelectedRecipe.Title}.";
            return false;
        }
        if (!SelectedRecipe.RequiresCommand && RecipeCommandName.Any(char.IsWhiteSpace))
        {
            error = "Enter a single command name without spaces, or leave it blank.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(EffectiveQueueName))
        {
            error = "Select or enter a queue name.";
            return false;
        }
        if (SelectedQueueIsDisabled)
        {
            error = PendingQueueIsDisabled
                ? "The new queue is disabled. Select another queue or create a new enabled queue."
                : "The selected queue is disabled. Choose an enabled queue or enable it in Actions > Queues.";
            return false;
        }
        foreach (var parameter in SelectedRecipe.Parameters.Where(parameter => parameter.Required))
        {
            if (string.IsNullOrWhiteSpace(GetParameter(parameter.Key)))
            {
                error = $"{parameter.Label} is required.";
                return false;
            }
        }

        return true;
    }

    private async Task CreateAsync()
    {
        if (_isBusy || !ValidateConfiguration(out _)) return;
        _isBusy = true;
        _error = string.Empty;
        Guid? newlyCreatedChannelPointRewardId = null;
        try
        {
            var recipe = SelectedRecipe!;
            var actionName = _actionName.Trim();
            var queueName = EffectiveQueueName;
            if (recipe is ITimerGroupActionRecipe timerRecipe)
            {
                await CreateTimerGroupActionsAsync(timerRecipe, actionName, queueName);
                return;
            }
            if (recipe is IRaffleSetupActionRecipe raffleRecipe)
            {
                await CreateRaffleSetupAsync(raffleRecipe, queueName);
                return;
            }

            var existingActions = await ActionService.GetActionSummariesAsync();
            if (existingActions.Any(action => string.Equals(action.Name, actionName, StringComparison.OrdinalIgnoreCase)))
            {
                _error = "An action with that name already exists.";
                return;
            }

            ActionCommand? command = null;
            if (!string.IsNullOrWhiteSpace(RecipeCommandName))
            {
                command = await CommandService.GetByCommandNameAsync(RecipeCommandName);
                if (command == null && await CommandService.CommandExistsAsync(RecipeCommandName))
                {
                    _error = $"The command !{RecipeCommandName} is already used by another command type.";
                    return;
                }
            }

            if (_pendingQueue is { } pendingQueue)
            {
                if (queueName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    _error = "Default is reserved. Choose another queue name.";
                    return;
                }
                if (_queues.Any(queue => queue.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase)))
                {
                    _error = "A queue with that name already exists. Select it instead.";
                    return;
                }

                var createdQueue = await QueueManager.CreateQueueAsync(pendingQueue);
                _queues.Add(createdQueue);
                _selectedQueueName = createdQueue.Name;
                _pendingQueue = null;
            }

            if (command == null && !string.IsNullOrWhiteSpace(RecipeCommandName))
                command = await CreateRecipeCommandAsync(RecipeCommandName, recipe);

            if (recipe is ChannelPointActionRecipe && IsCreatingChannelPointReward)
                newlyCreatedChannelPointRewardId = await CreateChannelPointRewardAsync();

            var action = recipe.Build(
                new ActionRecipeContext(actionName, queueName, command?.CommandName ?? string.Empty, command?.Id ?? Guid.Empty),
                _parameters);
            action.Group = EffectiveGroupName;
            action.Triggers ??= [];
            action.Triggers.AddRange(_additionalTriggers.Select(CloneTrigger));
            var createdAction = await ActionService.CreateActionAsync(action);
            newlyCreatedChannelPointRewardId = null;
            try
            {
                await RecipeTagStore.TagAsync(createdAction.Id, recipe.Id, _parameters);
            }
            catch (Exception tagException)
            {
                Snackbar.Add($"Action created, but its recipe tag could not be saved: {tagException.Message}", Severity.Warning);
            }

            Snackbar.Add($"Created action '{createdAction.Name}' from {recipe.Title}.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(createdAction.Id));
        }
        catch (Exception ex)
        {
            if (newlyCreatedChannelPointRewardId.HasValue)
            {
                try
                {
                    await TwitchService.DeleteChannelPointReward(newlyCreatedChannelPointRewardId.Value.ToString());
                }
                catch (Exception cleanupException)
                {
                    Snackbar.Add($"The action failed to save and the new Twitch reward could not be removed: {cleanupException.Message}", Severity.Warning);
                }
            }
            _error = $"Could not create the recipe action: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task<ActionCommand> CreateRecipeCommandAsync(string commandName, IActionRecipe recipe) =>
        await CommandService.AddAsync(new ActionCommand
        {
            CommandName = commandName,
            Category = $"Recipes / {recipe.Category}",
            Description = $"Created by recipe: {recipe.Id}",
            Disabled = false,
            MinimumRank = recipe.SupportsCommandSettings ? _commandMinimumRank : Rank.Viewer,
            UserCooldown = recipe.SupportsCommandSettings ? _commandUserCooldown : 0,
            GlobalCooldown = recipe.SupportsCommandSettings ? _commandGlobalCooldown : 0,
            SourceOnly = true
        });

    private Task<IEnumerable<string>> SearchGroups(string value, CancellationToken cancellationToken)
    {
        IEnumerable<string> groups = _groups;
        if (!string.IsNullOrWhiteSpace(SelectedRecipe?.SuggestedGroupName))
        {
            groups = groups.Append(SelectedRecipe.SuggestedGroupName);
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            groups = groups.Where(group => group.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
        return Task.FromResult<IEnumerable<string>>(groups.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private Task OnGroupChanged(string? groupName)
    {
        _groupName = groupName?.Trim() ?? string.Empty;
        return Task.CompletedTask;
    }
}

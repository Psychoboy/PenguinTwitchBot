using MudBlazor;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Recipes;
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
            error = "The selected queue is disabled. Choose an enabled queue or enable it in Actions > Queues.";
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
        try
        {
            var recipe = SelectedRecipe!;
            var actionName = _actionName.Trim();
            var queueName = EffectiveQueueName;
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
            {
                command = await CreateRecipeCommandAsync(RecipeCommandName, recipe);
            }

            var action = recipe.Build(
                new ActionRecipeContext(actionName, queueName, command?.CommandName ?? string.Empty, command?.Id ?? Guid.Empty),
                _parameters);
            action.Group = EffectiveGroupName;
            action.Triggers ??= [];
            action.Triggers.AddRange(_additionalTriggers.Select(CloneTrigger));
            var createdAction = await ActionService.CreateActionAsync(action);
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

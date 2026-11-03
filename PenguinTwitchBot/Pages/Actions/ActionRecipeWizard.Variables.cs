using System.Text.Json;
using PenguinTwitchBot.Bot.Actions.Variables;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<ActionVariableInfo> _availableVariables = [];
    private int _variableLoadVersion;

    private async Task LoadAvailableVariablesAsync()
    {
        var loadVersion = ++_variableLoadVersion;
        if (SelectedRecipe?.SupportsVariables != true)
        {
            _availableVariables = [];
            return;
        }

        var triggers = _additionalTriggers.Select(CloneTrigger).ToList();
        if (!string.IsNullOrWhiteSpace(RecipeCommandName))
        {
            var commandName = RecipeCommandName;
            triggers.Insert(0, new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                Enabled = true
            });
        }

        try
        {
            var variables = await VariableResolver.ResolveVariablesAsync(null, triggers, null);
            if (loadVersion == _variableLoadVersion)
            {
                _availableVariables = variables ?? [];
            }
        }
        catch
        {
            if (loadVersion == _variableLoadVersion)
            {
                _availableVariables = [];
            }
        }
    }

    private Task OnCommandNameChanged(string? commandName)
    {
        _commandName = commandName ?? string.Empty;
        return LoadAvailableVariablesAsync();
    }

    private Task OnRecipeParameterChanged(string key, string? value)
    {
        SetParameter(key, value);
        return string.Equals(key, "CommandName", StringComparison.OrdinalIgnoreCase)
            ? LoadAvailableVariablesAsync()
            : Task.CompletedTask;
    }
}
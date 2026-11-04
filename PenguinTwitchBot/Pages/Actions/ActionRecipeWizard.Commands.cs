using System.Text.Json;
using PenguinTwitchBot.Bot.Actions.Recipes;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<string> _commandNames = [];
    private List<string> _enabledCommandNames = [];

    private async Task LoadCommandNamesAsync()
    {
        _commandNames = [];
        _enabledCommandNames = [];
        if (SelectedRecipe is not (CommandAliasRecipe or CommandGroupToggleRecipe))
            return;

        var commands = await CommandService.GetAllAsync();
        _commandNames = commands
            .Select(command => command.CommandName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _enabledCommandNames = commands
            .Where(command => !command.Disabled)
            .Select(command => command.CommandName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task OnAliasTargetCommandChanged(string? commandName)
    {
        SetParameter("TargetCommand", commandName);
        return Task.CompletedTask;
    }

    private List<string> GetSelectedToggleCommands() =>
        CommandGroupToggleRecipe.ParseCommandNames(GetParameter("CommandNamesJson"));

    private Task OnToggleCommandsChanged(IEnumerable<string> commandNames)
    {
        SetParameter("CommandNamesJson", JsonSerializer.Serialize(commandNames));
        return Task.CompletedTask;
    }
}
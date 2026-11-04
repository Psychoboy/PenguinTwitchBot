using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class CommandGroupToggleRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("CommandNamesJson", "Commands", "Choose the commands to enable or disable."),
        new("Operation", "Change", "Choose whether to enable or disable the selected commands.", DefaultValue: "Disable")
    ];

    public string Id => "command-group-toggle";
    public string Title => "Enable or disable commands";
    public string Category => "Commands";
    public string Icon => "mdi-toggle-switch";
    public string Description => "Enable or disable a selected group of custom commands.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public string? SuggestedGroupName => "Recipes / Command Controls";

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        if (string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Action name and queue are required.", nameof(context));
        var names = ParseCommandNames(GetParameter(parameters, "CommandNamesJson"));
        if (names.Count == 0)
            throw new ArgumentException("Choose at least one command.", nameof(parameters));
        var operation = GetParameter(parameters, "Operation");
        if (!operation.Equals("Enable", StringComparison.OrdinalIgnoreCase) && !operation.Equals("Disable", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose Enable or Disable.", nameof(parameters));

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Command Controls",
            QueueName = context.QueueName,
            SubActions = names.Select((name, index) => (SubActionType)new ToggleCommandDisabledType
            {
                Index = index,
                CommandName = name,
                IsDisabled = operation.Equals("Disable", StringComparison.OrdinalIgnoreCase),
                Enabled = true
            }).ToList(),
            Triggers = []
        };
    }

    public static List<string> ParseCommandNames(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().TrimStart('!'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
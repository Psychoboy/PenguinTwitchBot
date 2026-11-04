using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class CommandAliasRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("TargetCommand", "Command to run", "Choose an existing custom command."),
        new("Arguments", "Arguments to pass", "Use %message% to pass the alias command's remaining text.", Required: false, DefaultValue: "%message%")
    ];

    public string Id => "command-alias";
    public string Title => "Command alias";
    public string Category => "Commands";
    public string Icon => "mdi-link-variant";
    public string Description => "Create a command that runs another custom command.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool SupportsCommandSettings => true;
    public bool SupportsVariables => true;
    public string? SuggestedGroupName => "Recipes / Command Aliases";
    public IReadOnlyList<string> VariableParameterKeys => ["Arguments"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var targetCommand = GetParameter(parameters, "TargetCommand").Trim().TrimStart('!');
        if (string.IsNullOrWhiteSpace(targetCommand))
            throw new ArgumentException("Choose a command to run.", nameof(parameters));

        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(context.ActionName) || string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Action name and queue are required.", nameof(context));
        if (string.IsNullOrWhiteSpace(commandName) || commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single alias command name is required.", nameof(context));
        if (context.CommandId == Guid.Empty)
            throw new ArgumentException("A persisted command ID is required.", nameof(context));

        return new ActionType
        {
            Name = context.ActionName.Trim(),
            Group = "Recipes / Command Aliases",
            QueueName = context.QueueName,
            SubActions =
            [
                new ExecuteCommandType
                {
                    Index = 0,
                    CommandName = targetCommand,
                    Text = GetParameter(parameters, "Arguments"),
                    Enabled = true
                }
            ],
            Triggers =
            [
                new TriggerType
                {
                    Name = $"!{commandName}",
                    Type = TriggerTypes.Command,
                    Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                    CommandId = context.CommandId,
                    Enabled = true
                }
            ]
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
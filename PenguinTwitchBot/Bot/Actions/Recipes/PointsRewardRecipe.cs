using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class PointsRewardRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("CommandName", "Chat command (optional)", "Enter a command without !, or leave blank to use another trigger or link this action later.", Required: false),
        new("PointTypeName", "Point type", "Choose an existing point type."),
        new("Operation", "Operation", "Choose how points are changed.", DefaultValue: nameof(ManagePointsOperation.Add)),
        new("Amount", "Amount or formula", "Supports event variables and $math(... )$ formulas.", DefaultValue: "10", ReplaceDefaultWhenInsertingVariable: true),
        new("TargetUser", "Target user", "Used for Add, Remove, and Set. Add Active ignores this field.", DefaultValue: "%user%", Required: false, ReplaceDefaultWhenInsertingVariable: true),
        new("MessageText", "Chat message (optional)", "Optional message sent after the points operation.", ActionRecipeParameterType.Multiline, Required: false)
    ];

    public string Id => "points-reward-command";
    public string Title => "Points reward or command";
    public string Category => "Points";
    public string Icon => "mdi-coins";
    public string Description => "Add, remove, set, or award points to active viewers from a command or event.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsCommandSettings => true;
    public bool SupportsVariables => true;
    public bool AllowsAdditionalTriggers => true;
    public string? SuggestedGroupName => "Recipes / Points";
    public IReadOnlyList<string> VariableParameterKeys => ["Amount", "TargetUser", "MessageText"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        var pointTypeName = GetParameter(parameters, "PointTypeName").Trim();
        var amount = GetParameter(parameters, "Amount").Trim();
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(pointTypeName))
            throw new ArgumentException("A point type is required.", nameof(parameters));
        if (string.IsNullOrWhiteSpace(amount))
            throw new ArgumentException("An amount or formula is required.", nameof(parameters));
        if (!Enum.TryParse<ManagePointsOperation>(GetParameter(parameters, "Operation"), true, out var operation))
            throw new ArgumentException("Select a valid points operation.", nameof(parameters));
        if (operation != ManagePointsOperation.AddActive && string.IsNullOrWhiteSpace(GetParameter(parameters, "TargetUser")))
            throw new ArgumentException("A target user is required for this operation.", nameof(parameters));
        if (commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));

        var triggers = new List<TriggerType>();
        if (!string.IsNullOrWhiteSpace(commandName))
        {
            if (context.CommandId == Guid.Empty)
                throw new ArgumentException("A persisted command ID is required.", nameof(context));

            triggers.Add(new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                CommandId = context.CommandId,
                Enabled = true
            });
        }

        var subActions = new List<SubActionType>
        {
            new ManagePointsType
            {
                Index = 0,
                PointTypeName = pointTypeName,
                Operation = operation.ToString(),
                Amount = amount,
                TargetUser = GetParameter(parameters, "TargetUser").Trim(),
                Enabled = true
            }
        };
        var message = GetParameter(parameters, "MessageText").Trim();
        if (!string.IsNullOrWhiteSpace(message))
            subActions.Add(new SendMessageType { Index = 1, Text = message, Enabled = true });

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Points",
            QueueName = context.QueueName,
            SubActions = subActions,
            Triggers = triggers
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
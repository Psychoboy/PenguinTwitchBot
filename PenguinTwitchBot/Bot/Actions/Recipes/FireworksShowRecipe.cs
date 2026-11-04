using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class FireworksShowRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new(
            "BurstCount",
            "Burst count",
            "Number of bursts (1-100). Supports %variables% and $math(expression)$.",
            DefaultValue: "10",
            ReplaceDefaultWhenInsertingVariable: true),
        new(
            "CommandName",
            "Chat command (optional)",
            "Enter a command without !, or leave blank to use another trigger or link this action later.",
            DefaultValue: "",
            Required: false),
        new(
            "DelayMilliseconds",
            "Delay between bursts (ms)",
            "Delay between bursts in milliseconds (0-60000). Supports %variables% and $math(expression)$.",
            DefaultValue: "100",
            ReplaceDefaultWhenInsertingVariable: true),
        new(
            "Shell",
            "Shell override (optional)",
            "Leave blank to use the Fireworks widget setting.",
            Required: false),
        new(
            "Size",
            "Size override (optional)",
            "Leave blank to use the Fireworks widget setting. Otherwise use 0-5.",
            Required: false)
    ];

    public string Id => "fireworks-show";
    public string Title => "Fireworks Show";
    public string Category => "Overlay";
    public string Icon => "mdi-firework";
    public string Description => "Create one editable fireworks action with a chat command or other trigger.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public bool SupportsVariables => true;
    public string? RecommendedQueueName => "Fireworks";
    public string? SuggestedGroupName => "Recipes / Fireworks";
    public IReadOnlyList<string> VariableParameterKeys => ["BurstCount", "DelayMilliseconds"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));
        if (!parameters.TryGetValue("BurstCount", out var burstCount) || string.IsNullOrWhiteSpace(burstCount))
            throw new ArgumentException("Burst count is required.", nameof(parameters));
        if (!parameters.TryGetValue("DelayMilliseconds", out var delay) || string.IsNullOrWhiteSpace(delay))
            throw new ArgumentException("Delay between bursts is required.", nameof(parameters));

        var triggers = new List<TriggerType>();
        if (!string.IsNullOrWhiteSpace(commandName) && context.CommandId != Guid.Empty)
        {
            triggers.Add(new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                CommandId = context.CommandId,
                Enabled = true
            });
        }

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Fireworks",
            QueueName = context.QueueName,
            SubActions =
            [
                new FireworksType
                {
                    Index = 0,
                    Count = burstCount.Trim(),
                    DelayMilliseconds = delay.Trim(),
                    Shell = parameters.GetValueOrDefault("Shell", string.Empty).Trim(),
                    Size = parameters.GetValueOrDefault("Size", string.Empty).Trim(),
                    Enabled = true
                }
            ],
            Triggers = triggers
        };
    }
}
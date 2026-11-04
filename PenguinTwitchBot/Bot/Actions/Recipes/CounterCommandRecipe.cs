using System.Globalization;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class CounterCommandRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("CounterName", "Counter", "Choose an existing counter."),
        new("Step", "Increment amount", "Positive integer to add each time the command runs.", DefaultValue: "1"),
        new("ResponseMessage", "Display message", "Message sent after incrementing the counter.", ActionRecipeParameterType.Multiline, "%counter_display_name%: %counter_value%", Required: false, ReplaceDefaultWhenInsertingVariable: true)
    ];

    public string Id => "counter-command";
    public string Title => "Counter command";
    public string Category => "Chat";
    public string Icon => "mdi-counter";
    public string Description => "Create an increment command for an existing counter and display its new value.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool SupportsCommandSettings => true;
    public bool SupportsVariables => true;
    public string? SuggestedGroupName => "Recipes / Counters";
    public IReadOnlyList<string> VariableParameterKeys => ["ResponseMessage"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var counterName = GetParameter(parameters, "CounterName").Trim();
        if (string.IsNullOrWhiteSpace(counterName))
            throw new ArgumentException("Choose an existing counter.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "Step"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var step) || step <= 0)
            throw new ArgumentException("Increment amount must be a positive integer.", nameof(parameters));

        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(context.ActionName) || string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Action name and queue are required.", nameof(context));
        if (string.IsNullOrWhiteSpace(commandName) || commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));
        if (context.CommandId == Guid.Empty)
            throw new ArgumentException("A persisted command ID is required.", nameof(context));

        var subActions = new List<SubActionType>
        {
            new MultiCounterType
            {
                Index = 0,
                Name = counterName,
                Operation = CounterOperation.Increment,
                Value = step,
                Enabled = true
            }
        };
        var responseMessage = GetParameter(parameters, "ResponseMessage").Trim();
        if (!string.IsNullOrWhiteSpace(responseMessage))
            subActions.Add(new SendMessageType { Index = 1, Text = responseMessage, Enabled = true });

        return new ActionType
        {
            Name = context.ActionName.Trim(),
            Group = "Recipes / Counters",
            QueueName = context.QueueName,
            SubActions = subActions,
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
using System.Globalization;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class MessageSequenceRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters = CreateParameters();

    public string Id => "message-sequence";
    public string Title => "Message sequence";
    public string Category => "Chat";
    public string Icon => "mdi-message-text-clock";
    public string Description => "Send up to five chat messages in order, with a configurable delay between each.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsVariables => true;
    public bool AllowsAdditionalTriggers => true;
    public string? SuggestedGroupName => "Recipes / Message Sequences";
    public IReadOnlyList<string> VariableParameterKeys => ["Message1", "Message2", "Message3", "Message4", "Message5"];

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

        var subActions = new List<SubActionType>();
        for (var messageNumber = 1; messageNumber <= 5; messageNumber++)
        {
            var message = GetParameter(parameters, $"Message{messageNumber}").Trim();
            if (string.IsNullOrWhiteSpace(message))
                continue;

            if (messageNumber > 1)
            {
                if (!int.TryParse(GetParameter(parameters, $"DelayBefore{messageNumber}"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay) || delay is < 0 or > 600000)
                    throw new ArgumentException($"Delay before message {messageNumber} must be between 0 and 600000 milliseconds.", nameof(parameters));

                subActions.Add(new DelayType
                {
                    Index = subActions.Count,
                    Duration = delay.ToString(CultureInfo.InvariantCulture),
                    Enabled = true
                });
            }

            subActions.Add(new SendMessageType
            {
                Index = subActions.Count,
                Text = message,
                Enabled = true
            });
        }

        if (!subActions.OfType<SendMessageType>().Any())
            throw new ArgumentException("Enter at least one chat message.", nameof(parameters));

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

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Message Sequences",
            QueueName = context.QueueName,
            SubActions = subActions,
            Triggers = triggers
        };
    }

    private static IReadOnlyList<ActionRecipeParameter> CreateParameters()
    {
        var parameters = new List<ActionRecipeParameter>
        {
            new("CommandName", "Chat command (optional)", "Enter a command without !, or leave blank to use another trigger or link this action later.", Required: false)
        };
        for (var messageNumber = 1; messageNumber <= 5; messageNumber++)
        {
            if (messageNumber > 1)
            {
                parameters.Add(new ActionRecipeParameter(
                    $"DelayBefore{messageNumber}",
                    $"Delay before message {messageNumber} (ms)",
                    "Delay from 0 to 600000 milliseconds.",
                    DefaultValue: "1000"));
            }

            parameters.Add(new ActionRecipeParameter(
                $"Message{messageNumber}",
                $"Message {messageNumber}",
                messageNumber == 1 ? "First message in the sequence." : "Leave blank to end the sequence here.",
                ActionRecipeParameterType.Multiline,
                Required: messageNumber == 1,
                ReplaceDefaultWhenInsertingVariable: true));
        }
        return parameters;
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
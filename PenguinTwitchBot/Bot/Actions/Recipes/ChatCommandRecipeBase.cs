using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public abstract class ChatCommandRecipeBase : IActionRecipe
{
    protected abstract string MessageParameterKey { get; }
    protected abstract string ActionGroup { get; }
    protected abstract ChatType CreateChatSubAction(string message);

    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Category { get; }
    public abstract string Icon { get; }
    public abstract string Description { get; }
    public abstract IReadOnlyList<ActionRecipeParameter> Parameters { get; }
    public bool SupportsVariables => true;

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(commandName) || commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));
        if (context.CommandId == Guid.Empty)
            throw new ArgumentException("A persisted command ID is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (!parameters.TryGetValue(MessageParameterKey, out var message) || string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message text is required.", nameof(parameters));

        var subAction = CreateChatSubAction(message.Trim());
        subAction.Index = 0;
        subAction.Enabled = true;

        return new ActionType
        {
            Name = actionName,
            Group = ActionGroup,
            QueueName = context.QueueName,
            SubActions = [subAction],
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

    public static string NormalizeCommandName(string? commandName) =>
        (commandName ?? string.Empty).Trim().TrimStart('!').ToLowerInvariant();
}
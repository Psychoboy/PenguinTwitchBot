using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class ChatReplyRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new(
            "ReplyText",
            "Reply text",
            "Supports action variables such as %user% and %message%.",
            ActionRecipeParameterType.Multiline)
    ];

    public string Id => "chat-command-reply";
    public string Title => "Chat command reply";
    public string Category => "Chat";
    public string Icon => "mdi-chat-processing-outline";
    public string Description => "Create a command that replies to the triggering chat message.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("Command name is required.", nameof(context));
        if (context.CommandId == Guid.Empty)
            throw new ArgumentException("A persisted command ID is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (!parameters.TryGetValue("ReplyText", out var replyText) || string.IsNullOrWhiteSpace(replyText))
            throw new ArgumentException("Reply text is required.", nameof(parameters));

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Chat Replies",
            QueueName = context.QueueName,
            SubActions =
            [
                new ReplyToMessageType
                {
                    Index = 0,
                    Text = replyText.Trim(),
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

    public static string NormalizeCommandName(string? commandName) =>
        (commandName ?? string.Empty).Trim().TrimStart('!').ToLowerInvariant();
}
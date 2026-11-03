using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class ChatReplyRecipe : ChatCommandRecipeBase
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new(
            "ReplyText",
            "Reply text",
            "Supports action variables such as %user% and %message%.",
            ActionRecipeParameterType.Multiline)
    ];

    public override string Id => "chat-command-reply";
    public override string Title => "Chat command reply";
    public override string Category => "Chat";
    public override string Icon => "mdi-chat-processing-outline";
    public override string Description => "Create a command that replies to the triggering chat message.";
    public override IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    protected override string MessageParameterKey => "ReplyText";
    protected override string ActionGroup => "Recipes / Chat Replies";

    protected override ChatType CreateChatSubAction(string message) => new ReplyToMessageType { Text = message };

    public new static string NormalizeCommandName(string? commandName) =>
        ChatCommandRecipeBase.NormalizeCommandName(commandName);
}
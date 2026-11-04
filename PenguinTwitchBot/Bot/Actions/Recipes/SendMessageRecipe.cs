using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class SendMessageRecipe : ChatCommandRecipeBase
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new(
            "MessageText",
            "Message text",
            "Supports action variables such as %user% and %message%.",
            ActionRecipeParameterType.Multiline)
    ];

    public override string Id => "chat-command-send-message";
    public override string Title => "Send Message";
    public override string Category => "Chat";
    public override string Icon => "mdi-message-text-outline";
    public override string Description => "Create a command that sends a message to chat.";
    public override IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    protected override string MessageParameterKey => "MessageText";
    protected override string ActionGroup => "Recipes / Chat Messages";

    protected override ChatType CreateChatSubAction(string message) => new SendMessageType { Text = message };
}
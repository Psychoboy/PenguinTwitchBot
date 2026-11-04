using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class KeywordReactionRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("MessageText", "Chat message", "Message to send when the keyword is detected.", ActionRecipeParameterType.Multiline, "Thanks, %user%!", ReplaceDefaultWhenInsertingVariable: true),
        new("SoundFile", "Sound file (optional)", "Optional audio file to play with the response.", Required: false)
    ];

    public string Id => "keyword-reaction";
    public string Title => "Keyword reaction";
    public string Category => "Chat";
    public string Icon => "mdi-text-search";
    public string Description => "Reply to a configured chat keyword, optionally with a sound.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsVariables => true;
    public bool AllowsAdditionalTriggers => true;
    public bool RequiresTrigger => true;
    public string TriggerRequirementDescription => "Keyword trigger";
    public string TriggerButtonLabel => "Add keyword trigger";
    public IReadOnlyCollection<TriggerTypes>? AllowedTriggerTypes => [TriggerTypes.Keyword];
    public string? SuggestedGroupName => "Recipes / Keywords";
    public IReadOnlyList<string> VariableParameterKeys => ["MessageText"];

    public bool IsRequiredTrigger(TriggerType trigger) => trigger.Type == TriggerTypes.Keyword;

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var message = GetParameter(parameters, "MessageText").Trim();
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A chat message is required.", nameof(parameters));

        var subActions = new List<SubActionType>
        {
            new SendMessageType { Index = 0, Text = message, Enabled = true }
        };
        var soundFile = GetParameter(parameters, "SoundFile").Trim();
        if (!string.IsNullOrWhiteSpace(soundFile))
            subActions.Add(new PlaySoundType { Index = 1, File = soundFile, Enabled = true });

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Keywords",
            QueueName = context.QueueName,
            SubActions = subActions,
            Triggers = []
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
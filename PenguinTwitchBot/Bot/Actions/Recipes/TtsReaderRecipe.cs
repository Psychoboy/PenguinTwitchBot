using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class TtsReaderRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("CommandName", "Chat command (optional)", "Enter a command without !, or leave blank to use another trigger or link this action later.", Required: false),
        new("VoiceUsername", "Voice username", "Username whose configured voice should be used. Leave %user% to use the triggering chatter's voice.", DefaultValue: "%user%", Required: false, ReplaceDefaultWhenInsertingVariable: true),
        new("SpeechText", "Text to read", "Use %message% to read the command or trigger message, or insert another event variable.", ActionRecipeParameterType.Multiline, "%message%", ReplaceDefaultWhenInsertingVariable: true)
    ];

    public string Id => "tts-reader";
    public string Title => "TTS reader";
    public string Category => "Audio";
    public string Icon => "mdi-account-voice";
    public string Description => "Read a command or event message aloud using the triggering user's configured voice.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsCommandSettings => true;
    public bool SupportsVariables => true;
    public bool AllowsAdditionalTriggers => true;
    public string? SuggestedGroupName => "Recipes / TTS";
    public IReadOnlyList<string> VariableParameterKeys => ["VoiceUsername", "SpeechText"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        var speechText = GetParameter(parameters, "SpeechText").Trim();
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(speechText))
            throw new ArgumentException("Text to read is required.", nameof(parameters));
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

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / TTS",
            QueueName = context.QueueName,
            SubActions =
            [
                new TtsType
                {
                    Index = 0,
                    Name = GetParameter(parameters, "VoiceUsername").Trim(),
                    Text = speechText,
                    Enabled = true
                }
            ],
            Triggers = triggers
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
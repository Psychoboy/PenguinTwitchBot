using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class SoundCommandRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("File", "Audio file", "Choose an audio file from the Media Library.")
    ];

    public string Id => "sound-command";
    public string Title => "Sound command";
    public string Category => "Audio";
    public string Icon => "mdi-volume-high";
    public string Description => "Create a chat command that plays a selected audio file.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool SupportsCommandSettings => true;

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(commandName) || commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));
        if (context.CommandId == Guid.Empty)
            throw new ArgumentException("A persisted command ID is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (!parameters.TryGetValue("File", out var file) || string.IsNullOrWhiteSpace(file))
            throw new ArgumentException("An audio file is required.", nameof(parameters));

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Sounds",
            QueueName = context.QueueName,
            SubActions =
            [
                new PlaySoundType
                {
                    Index = 0,
                    File = file.Trim(),
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
}
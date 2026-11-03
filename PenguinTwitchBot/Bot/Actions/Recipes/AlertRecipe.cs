using System.Globalization;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class AlertRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new(
            "CommandName",
            "Chat command (optional)",
            "Enter a command without !, or leave blank to use another trigger or link this action later.",
            DefaultValue: "",
            Required: false)
    ];

    public string Id => "alert";
    public string Title => "Alert";
    public string Category => "Overlay";
    public string Icon => "mdi-bell-alert-outline";
    public string Description => "Create an editable alert action with media, styling, and a live overlay preview.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public bool SupportsVariables => true;

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

        var text = GetParameter(parameters, "AlertText");
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Alert text is required.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "Duration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration) || duration is < 1 or > 60)
            throw new ArgumentException("Alert duration must be between 1 and 60 seconds.", nameof(parameters));
        if (!float.TryParse(GetParameter(parameters, "Volume"), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume) || volume is < 0 or > 1)
            throw new ArgumentException("Alert volume must be between 0 and 1.", nameof(parameters));

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
            Group = "Recipes / Alerts",
            QueueName = context.QueueName,
            SubActions =
            [
                new AlertType
                {
                    Index = 0,
                    Text = text.Trim(),
                    File = GetParameter(parameters, "File").Trim(),
                    AudioFile = GetParameter(parameters, "AudioFile").Trim(),
                    Duration = duration,
                    Volume = volume,
                    CSS = GetParameter(parameters, "CSS"),
                    AlertChannel = GetParameter(parameters, "AlertChannel").Trim(),
                    Enabled = bool.TryParse(GetParameter(parameters, "Enabled"), out var enabled) ? enabled : true
                }
            ],
            Triggers = triggers
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
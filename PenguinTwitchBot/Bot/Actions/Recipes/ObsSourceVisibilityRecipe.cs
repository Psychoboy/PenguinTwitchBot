using System.Globalization;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class ObsSourceVisibilityRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("CommandName", "Chat command (optional)", "Enter a command without !, or leave blank to use another trigger or link this action later.", Required: false),
        new("OBSConnectionId", "OBS connection", "Select a configured OBS connection."),
        new("SceneName", "Scene", "Select a scene from the connected OBS instance."),
        new("SourceName", "Source", "Select a source in the chosen scene."),
        new("DurationSeconds", "Visible for (seconds)", "Choose how long the source stays visible, from 1 to 3600 seconds.", DefaultValue: "10")
    ];

    public string Id => "obs-source-visibility";
    public string Title => "Show OBS source";
    public string Category => "OBS";
    public string Icon => "mdi-eye-time";
    public string Description => "Show an OBS source for a chosen duration, then hide it again.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public string? SuggestedGroupName => "Recipes / OBS";

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        var connectionIdText = GetParameter(parameters, "OBSConnectionId");
        if (!Guid.TryParse(connectionIdText, out var connectionId) || connectionId == Guid.Empty)
            throw new ArgumentException("A configured OBS connection is required.", nameof(parameters));
        var sceneName = GetParameter(parameters, "SceneName").Trim();
        var sourceName = GetParameter(parameters, "SourceName").Trim();
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("An OBS scene and source are required.", nameof(parameters));
        if (commandName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A single command name is required.", nameof(context));
        if (!int.TryParse(GetParameter(parameters, "DurationSeconds"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationSeconds) || durationSeconds is < 1 or > 3600)
            throw new ArgumentException("Visibility duration must be between 1 and 3600 seconds.", nameof(parameters));

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

        var durationMilliseconds = (durationSeconds * 1000).ToString(CultureInfo.InvariantCulture);
        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / OBS",
            QueueName = context.QueueName,
            SubActions =
            [
                new ObsSetSourceVisibilityType
                {
                    Index = 0,
                    OBSConnectionId = connectionId,
                    SceneName = sceneName,
                    SourceName = sourceName,
                    Visible = true,
                    Enabled = true
                },
                new DelayType
                {
                    Index = 1,
                    Duration = durationMilliseconds,
                    Enabled = true
                },
                new ObsSetSourceVisibilityType
                {
                    Index = 2,
                    OBSConnectionId = connectionId,
                    SceneName = sceneName,
                    SourceName = sourceName,
                    Visible = false,
                    Enabled = true
                }
            ],
            Triggers = triggers
        };
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}
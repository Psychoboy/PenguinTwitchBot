using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

[SubActionMetadata(
    displayName: "Stop Starship Expedition",
    description: "Stops the currently active Starship Expedition",
    icon: "mdi-rocket-launch-outline",
    color: "Warning",
    category: SubActionCategories.Minigames,
    tableName: "subactions_starship_stop_expedition")]
public sealed class StarshipStopExpeditionType : SubActionType, ISubActionUIProvider
{
    public StarshipStopExpeditionType() => SubActionTypes = SubActionTypes.StarshipStopExpedition;

    public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
    [
        new()
        {
            PropertyName = nameof(Enabled),
            Label = "Enabled",
            FieldType = UIFieldType.Switch,
            SwitchColor = "Success"
        }
    ];

    public Dictionary<string, object?> GetValues() => new() { [nameof(Enabled)] = Enabled };

    public void SetValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
    }

    public string? Validate(Dictionary<string, object?> values) => null;
}

using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

[SubActionMetadata(
    displayName: "Start Starship Expedition",
    description: "Starts Starship Expedition using its saved game settings",
    icon: "mdi-rocket-launch",
    color: "Success",
    category: SubActionCategories.Minigames,
    tableName: "subactions_starship_start_expedition")]
public sealed class StarshipStartExpeditionType : SubActionType, ISubActionUIProvider
{
    public StarshipStartExpeditionType() => SubActionTypes = SubActionTypes.StarshipStartExpedition;

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

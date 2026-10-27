using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

[SubActionMetadata(
    displayName: "Start Starship Expedition",
    description: "Starts a Starship Expedition using saved game settings or optional custom overrides",
    icon: "mdi-rocket-launch",
    color: "Success",
    category: SubActionCategories.Minigames,
    minigameGroup: MinigameGroups.Starship,
    tableName: "subactions_starship_start_expedition")]
public sealed class StarshipStartExpeditionType : SubActionType, ISubActionUIProvider
{
    public StarshipStartExpeditionType() => SubActionTypes = SubActionTypes.StarshipStartExpedition;

    public string LegCount { get; set; } = "";
    public string MinEventIntervalMinutes { get; set; } = "";
    public string MaxEventIntervalMinutes { get; set; } = "";
    public string Seed { get; set; } = "";

    public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
    [
        new()
        {
            PropertyName = nameof(LegCount),
            Label = "Leg Count Override (Optional)",
            FieldType = UIFieldType.Text,
            Required = false,
            HelperText = "Number of voyage legs (e.g. 3 - 10). Leave blank to use saved Starship Expedition settings. Supports %variables%."
        },
        new()
        {
            PropertyName = nameof(MinEventIntervalMinutes),
            Label = "Min Event Interval Minutes (Optional)",
            FieldType = UIFieldType.Text,
            Required = false,
            HelperText = "Minimum minutes between expedition events. Leave blank to use saved game settings."
        },
        new()
        {
            PropertyName = nameof(MaxEventIntervalMinutes),
            Label = "Max Event Interval Minutes (Optional)",
            FieldType = UIFieldType.Text,
            Required = false,
            HelperText = "Maximum minutes between expedition events. Leave blank to use saved game settings."
        },
        new()
        {
            PropertyName = nameof(Seed),
            Label = "Random Seed Override (Optional)",
            FieldType = UIFieldType.Text,
            Required = false,
            HelperText = "Optional integer seed for deterministic voyage generation. Leave blank for random."
        },
        new()
        {
            PropertyName = nameof(Enabled),
            Label = "Enabled",
            FieldType = UIFieldType.Switch,
            SwitchColor = "Success"
        }
    ];

    public Dictionary<string, object?> GetValues() => new()
    {
        [nameof(LegCount)] = LegCount,
        [nameof(MinEventIntervalMinutes)] = MinEventIntervalMinutes,
        [nameof(MaxEventIntervalMinutes)] = MaxEventIntervalMinutes,
        [nameof(Seed)] = Seed,
        [nameof(Enabled)] = Enabled
    };

    public void SetValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(LegCount), out var legCount)) LegCount = legCount as string ?? "";
        if (values.TryGetValue(nameof(MinEventIntervalMinutes), out var minInterval)) MinEventIntervalMinutes = minInterval as string ?? "";
        if (values.TryGetValue(nameof(MaxEventIntervalMinutes), out var maxInterval)) MaxEventIntervalMinutes = maxInterval as string ?? "";
        if (values.TryGetValue(nameof(Seed), out var seed)) Seed = seed as string ?? "";
        if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
    }

    public string? Validate(Dictionary<string, object?> values) => null;
}

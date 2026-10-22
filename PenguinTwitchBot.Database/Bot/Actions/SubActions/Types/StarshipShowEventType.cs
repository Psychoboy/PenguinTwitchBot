using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

[SubActionMetadata(
    displayName: "Starship Show Event Widget",
    description: "Keeps the large Starship Expedition event widget visible for a period",
    icon: "mdi-monitor-eye",
    color: "Info",
    category: SubActionCategories.Minigames,
    tableName: "subactions_starship_show_event")]
public sealed class StarshipShowEventType : SubActionType, ISubActionUIProvider
{
    public StarshipShowEventType() => SubActionTypes = SubActionTypes.StarshipShowEvent;

    public string DurationSeconds { get; set; } = "30";

    public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
    [
        new()
        {
            PropertyName = nameof(DurationSeconds),
            Label = "Show Duration (Seconds)",
            FieldType = UIFieldType.Text,
            Required = true,
            HelperText = "1-3600 seconds. The event widget hides again when this expires. Supports variables."
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
        { nameof(DurationSeconds), DurationSeconds },
        { nameof(Enabled), Enabled }
    };

    public void SetValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(DurationSeconds), out var duration)) DurationSeconds = duration as string ?? "30";
        if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var duration = values.TryGetValue(nameof(DurationSeconds), out var value) ? value as string : DurationSeconds;
        duration = duration?.Trim();
        if (string.IsNullOrWhiteSpace(duration)) return "A show duration is required.";
        if (!duration.Contains('%') && !duration.Contains('$') &&
            (!int.TryParse(duration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 3600))
            return "Show duration must be a whole number of seconds (1-3600).";
        return null;
    }
}

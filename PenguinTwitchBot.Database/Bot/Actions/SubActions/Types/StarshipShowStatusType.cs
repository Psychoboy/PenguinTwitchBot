using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Starship Show Status Widget",
        description: "Keeps the Starship Expedition status widget visible for a period, even while the voyage is cruising",
        icon: "mdi-rocket-launch",
        color: "Info",
        category: SubActionCategories.Minigames,
        tableName: "subactions_starship_show_status")]
    public class StarshipShowStatusType : SubActionType, ISubActionUIProvider
    {
        public StarshipShowStatusType()
        {
            SubActionTypes = SubActionTypes.StarshipShowStatus;
        }

        /// <summary>
        /// How long to force the widget visible, in seconds. Supports variables.
        /// </summary>
        public string DurationSeconds { get; set; } = "30";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(DurationSeconds),
                    Label = "Show Duration (Seconds)",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "1-3600 seconds. If the voyage is still cruising afterwards, the widget fades out again. Supports variables."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

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
            if (string.IsNullOrWhiteSpace(duration))
            {
                return "A show duration is required.";
            }
            if (!duration.Contains('%') && !duration.Contains('$') &&
                (!int.TryParse(duration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 3600))
            {
                return "Show duration must be a whole number of seconds (1-3600).";
            }
            return null;
        }
    }
}

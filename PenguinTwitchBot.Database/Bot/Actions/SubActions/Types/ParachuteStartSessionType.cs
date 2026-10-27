using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Start Parachute Session",
        description: "Starts a timed Parachute Drop session on the overlay",
        icon: "mdi-timer-play",
        color: "Success",
        category: SubActionCategories.Minigames,
        minigameGroup: MinigameGroups.Parachute,
        tableName: "subactions_parachute_start_session")]
    public class ParachuteStartSessionType : SubActionType, ISubActionUIProvider
    {
        public ParachuteStartSessionType()
        {
            SubActionTypes = SubActionTypes.ParachuteStartSession;
        }

        public string DurationSeconds { get; set; } = "100";
        public string ThemeOverrideId { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(DurationSeconds),
                Label = "Session Duration (Seconds)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Duration of the Parachute Drop round in seconds (e.g. 100). Leave blank to use saved game settings. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(ThemeOverrideId),
                Label = "Theme Override",
                FieldType = UIFieldType.Select,
                AllowCustomValue = true,
                HelperText = "Optional theme override for this session (leave default to use active theme).",
                SelectOptions =
                [
                    new SelectOption { Value = "", Name = "Use Active Game Theme" },
                    new SelectOption { Value = "spring", Name = "Spring (Cherry Blossoms)" },
                    new SelectOption { Value = "summer", Name = "Summer (Tropical Beach)" },
                    new SelectOption { Value = "fall", Name = "Fall (Autumn Harvest)" },
                    new SelectOption { Value = "winter", Name = "Winter (Snowy Frost)" },
                    new SelectOption { Value = "halloween", Name = "Halloween (Spooky Cauldron)" },
                    new SelectOption { Value = "christmas", Name = "Christmas (Holiday Celebration)" }
                ]
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
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(DurationSeconds), out var duration)) DurationSeconds = duration as string ?? "";
            if (values.TryGetValue(nameof(ThemeOverrideId), out var theme)) ThemeOverrideId = theme as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}


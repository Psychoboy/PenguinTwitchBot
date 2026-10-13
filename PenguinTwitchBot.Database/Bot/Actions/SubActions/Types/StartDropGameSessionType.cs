using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Start Drop Game Session",
        description: "Starts a timed round/session for Parachute Drop or Plinko Bounce (e.g. 100 seconds)",
        icon: "mdi-timer-play",
        color: "Success",
        category: SubActionCategories.Minigames,
        tableName: "subactions_start_drop_game_session")]
    public class StartDropGameSessionType : SubActionType, ISubActionUIProvider
    {
        public StartDropGameSessionType()
        {
            SubActionTypes = SubActionTypes.StartDropGameSession;
        }

        /// <summary>
        /// Minigame to start: "Parachute" or "Plinko".
        /// </summary>
        public string GameType { get; set; } = "Parachute";

        /// <summary>
        /// Duration of the round in seconds (e.g. 100). Supports variables like %Duration%. Blank uses default setting.
        /// </summary>
        public string DurationSeconds { get; set; } = "100";

        /// <summary>
        /// Optional theme ID override for this session ("spring", "summer", "fall", "winter", or blank for default).
        /// </summary>
        public string ThemeOverrideId { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(GameType),
                    Label = "Minigame",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new SelectOption { Value = "Parachute", Name = "Parachute Drop (!drop)" },
                        new SelectOption { Value = "Plinko", Name = "Plinko Bounce (!plinko)" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(DurationSeconds),
                    Label = "Session Duration (Seconds)",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Duration of the round in seconds (e.g. 100). Supports variables like %Duration%."
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
                        new SelectOption { Value = "winter", Name = "Winter (Snowy Frost)" }
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
        }

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(GameType), GameType },
            { nameof(DurationSeconds), DurationSeconds },
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(GameType), out var gameType)) GameType = gameType as string ?? "Parachute";
            if (values.TryGetValue(nameof(DurationSeconds), out var duration)) DurationSeconds = duration as string ?? "100";
            if (values.TryGetValue(nameof(ThemeOverrideId), out var theme)) ThemeOverrideId = theme as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(GameType), out var gameType) && string.IsNullOrWhiteSpace(gameType as string))
            {
                return "Minigame selection is required.";
            }
            return null;
        }
    }
}


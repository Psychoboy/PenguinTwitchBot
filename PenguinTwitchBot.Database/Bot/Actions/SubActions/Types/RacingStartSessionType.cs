using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Start Race Lobby",
        description: "Opens a Racing or Destruction Derby lobby on the overlay with configurable mode, track, laps, and lobby duration",
        icon: "mdi-flag-checkered",
        color: "Success",
        category: SubActionCategories.Minigames,
        minigameGroup: MinigameGroups.Racing,
        tableName: "subactions_racing_start_session")]
    public class RacingStartSessionType : SubActionType, ISubActionUIProvider
    {
        public RacingStartSessionType()
        {
            SubActionTypes = SubActionTypes.RacingStartSession;
        }

        public string GameMode { get; set; } = "";
        public string TrackType { get; set; } = "";
        public string TotalLaps { get; set; } = "";
        public string DurationSeconds { get; set; } = "";
        public string ThemeOverrideId { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(GameMode),
                Label = "Race Mode",
                FieldType = UIFieldType.Select,
                AllowCustomValue = true,
                HelperText = "Select the racing mode or leave default to use saved game settings. Supports %variables%.",
                SelectOptions =
                [
                    new SelectOption { Value = "", Name = "Default (Use Saved Game Settings)" },
                    new SelectOption { Value = "GrandPrix", Name = "Grand Prix (Standard Lap Race)" },
                    new SelectOption { Value = "Elimination", Name = "Elimination (Last-Place Knockout)" },
                    new SelectOption { Value = "DestructionDerby", Name = "Destruction Derby (Arena Survival)" }
                ]
            },
            new()
            {
                PropertyName = nameof(TrackType),
                Label = "Track Layout",
                FieldType = UIFieldType.Select,
                AllowCustomValue = true,
                HelperText = "Select the track layout or leave default to use saved game settings.",
                SelectOptions =
                [
                    new SelectOption { Value = "", Name = "Default (Use Saved Game Settings)" },
                    new SelectOption { Value = "Perimeter", Name = "Perimeter Circuit (Full Screen Border)" },
                    new SelectOption { Value = "EdgeOval", Name = "Classic Oval Speedway" },
                    new SelectOption { Value = "Figure8", Name = "Figure-8 Crossover" },
                    new SelectOption { Value = "DragStrip", Name = "Sprint Drag Strip" },
                    new SelectOption { Value = "DerbyBowl", Name = "Destruction Derby Arena Bowl" }
                ]
            },
            new()
            {
                PropertyName = nameof(TotalLaps),
                Label = "Total Laps Override (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Number of laps (1 - 10) for Grand Prix / Elimination. Leave blank to use saved game settings. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(DurationSeconds),
                Label = "Lobby Countdown Duration (Seconds)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "How long the race lobby stays open for !join before launching (e.g. 45). Leave blank to use saved game settings."
            },
            new()
            {
                PropertyName = nameof(ThemeOverrideId),
                Label = "Theme Override",
                FieldType = UIFieldType.Select,
                AllowCustomValue = true,
                HelperText = "Optional theme override for this race session.",
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
            { nameof(GameMode), GameMode },
            { nameof(TrackType), TrackType },
            { nameof(TotalLaps), TotalLaps },
            { nameof(DurationSeconds), DurationSeconds },
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(GameMode), out var mode)) GameMode = mode as string ?? "";
            if (values.TryGetValue(nameof(TrackType), out var track)) TrackType = track as string ?? "";
            if (values.TryGetValue(nameof(TotalLaps), out var laps)) TotalLaps = laps as string ?? "";
            if (values.TryGetValue(nameof(DurationSeconds), out var duration)) DurationSeconds = duration as string ?? "";
            if (values.TryGetValue(nameof(ThemeOverrideId), out var theme)) ThemeOverrideId = theme as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}


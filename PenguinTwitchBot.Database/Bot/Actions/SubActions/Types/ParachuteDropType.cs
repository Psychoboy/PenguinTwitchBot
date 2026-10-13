using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Parachute Drop",
        description: "Drops a viewer's character or emote onto the Parachute Drop overlay minigame",
        icon: "mdi-parachute",
        color: "Info",
        category: SubActionCategories.Minigames,
        tableName: "subactions_parachute_drop")]
    public class ParachuteDropType : SubActionType, ISubActionUIProvider
    {
        public ParachuteDropType()
        {
            SubActionTypes = SubActionTypes.ParachuteDrop;
        }

        /// <summary>
        /// Username of the player dropping. Supports %User% and other action variables.
        /// </summary>
        public string Username { get; set; } = "%User%";

        /// <summary>
        /// Optional emote name, emote URL, or %Args% variable. When empty or no emote matches, uses the theme's default character.
        /// </summary>
        public string EmoteOrImage { get; set; } = "%Args%";

        /// <summary>
        /// Optional seasonal or custom theme ID override (e.g., "spring", "summer", "fall", "winter", or blank for active theme).
        /// </summary>
        public string ThemeOverrideId { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Username),
                    Label = "Player Username",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Username displayed on the parachute drop. Defaults to %User%."
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Emote / Character Image (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Emote code, image URL, or %Args%. Leave blank or use %Args% to fall back to the theme's default character when no emote is provided."
                },
                new()
                {
                    PropertyName = nameof(ThemeOverrideId),
                    Label = "Theme Override",
                    FieldType = UIFieldType.Select,
                    AllowCustomValue = true,
                    HelperText = "Optional theme override for this drop (leave default to use the active Parachute Drop theme).",
                    SelectOptions =
                    [
                        new SelectOption { Value = "", Name = "Use Active Theme" },
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
            { nameof(Username), Username },
            { nameof(EmoteOrImage), EmoteOrImage },
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var username)) Username = username as string ?? "%User%";
            if (values.TryGetValue(nameof(EmoteOrImage), out var emote)) EmoteOrImage = emote as string ?? "";
            if (values.TryGetValue(nameof(ThemeOverrideId), out var theme)) ThemeOverrideId = theme as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var username) && string.IsNullOrWhiteSpace(username as string))
            {
                return "Player Username is required (use %User% for the triggering viewer).";
            }
            return null;
        }
    }
}


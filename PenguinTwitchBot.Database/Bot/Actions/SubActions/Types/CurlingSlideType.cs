using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Curling / Shuffleboard Slide",
        description: "Delivers a stone or puck down the ice/runway on the Curling / Shuffleboard overlay minigame",
        icon: "mdi-hockey-puck",
        color: "Info",
        category: SubActionCategories.Minigames,
        tableName: "subactions_curling_slide")]
    public class CurlingSlideType : SubActionType, ISubActionUIProvider
    {
        public CurlingSlideType()
        {
            SubActionTypes = SubActionTypes.CurlingSlide;
        }

        public string Username { get; set; } = "%User%";
        public string Power { get; set; } = "";
        public string EmoteOrImage { get; set; } = "%Args%";
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
                    HelperText = "Username displayed on the curling stone. Defaults to %User%."
                },
                new()
                {
                    PropertyName = nameof(Power),
                    Label = "Launch Power (1-100)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Delivery power between 1 and 100, or variable like %Power%. Leave blank for random calibrated power."
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Emote / Decal Image (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Emote code, %EmoteUrl%, image URL, or %Args%. Leave blank to fall back to the chatter's avatar."
                },
                new()
                {
                    PropertyName = nameof(ThemeOverrideId),
                    Label = "Theme Override",
                    FieldType = UIFieldType.Select,
                    AllowCustomValue = true,
                    HelperText = "Optional theme override (leave blank to use active theme).",
                    SelectOptions =
                    [
                        new SelectOption { Value = "", Name = "Active Theme (Default)" },
                        new SelectOption { Value = "spring", Name = "Spring (Cherry Blossoms)" },
                        new SelectOption { Value = "summer", Name = "Summer (Tropical Pool)" },
                        new SelectOption { Value = "fall", Name = "Fall (Autumn Leaves)" },
                        new SelectOption { Value = "winter", Name = "Winter (Snowy Frost)" },
                        new SelectOption { Value = "halloween", Name = "Halloween (Spooky Night)" },
                        new SelectOption { Value = "christmas", Name = "Christmas (Holiday Fest)" }
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
            { nameof(Power), Power },
            { nameof(EmoteOrImage), EmoteOrImage },
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var username)) Username = username as string ?? "%User%";
            if (values.TryGetValue(nameof(Power), out var power)) Power = power as string ?? "";
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

        public string GetDescription()
        {
            var powerStr = !string.IsNullOrWhiteSpace(Power) ? $" with power {Power}" : "";
            var emoteStr = !string.IsNullOrWhiteSpace(EmoteOrImage) && !EmoteOrImage.Equals("%Args%", StringComparison.OrdinalIgnoreCase)
                ? $" (decal: {EmoteOrImage})"
                : "";
            return $"Slide curling stone for {Username}{powerStr}{emoteStr}";
        }
    }
}


using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Golf Chip & Putt Shot",
        description: "Strikes a golf ball onto the putting green on the Golf Chip & Putt overlay minigame",
        icon: "mdi-golf",
        color: "Success",
        category: SubActionCategories.Minigames,
        tableName: "subactions_golf_putt")]
    public class GolfPuttType : SubActionType, ISubActionUIProvider
    {
        public GolfPuttType()
        {
            SubActionTypes = SubActionTypes.GolfPutt;
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
                    HelperText = "Username displayed on the golf ball. Defaults to %User%."
                },
                new()
                {
                    PropertyName = nameof(Power),
                    Label = "Swing Power (1-100)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Swing/putt power between 1 and 100, or variable like %Power%. Leave blank for random calibrated power."
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Emote / Ball Decal (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Emote code, %EmoteUrl%, image URL, or %Args%. Leave blank to use default golf ball art."
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

            if (values.TryGetValue(nameof(Power), out var powerObj) && powerObj is string powerStr && !string.IsNullOrWhiteSpace(powerStr))
            {
                var trimmed = powerStr.Trim();
                if (!trimmed.StartsWith('%') && (!double.TryParse(trimmed, out var parsed) || parsed < 1 || parsed > 100))
                {
                    return "Swing Power must be a number between 1 and 100, a variable (e.g. %Power%), or left blank.";
                }
            }

            return null;
        }
    }
}


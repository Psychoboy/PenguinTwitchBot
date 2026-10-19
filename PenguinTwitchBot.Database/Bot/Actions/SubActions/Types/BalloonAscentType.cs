using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Balloon Ascent",
        description: "Releases a viewer's character or emote with a floating balloon on the Balloon Ascent overlay minigame",
        icon: "mdi-balloon",
        color: "Info",
        category: SubActionCategories.Minigames,
        tableName: "subactions_balloon_ascent")]
    public class BalloonAscentType : SubActionType, ISubActionUIProvider
    {
        public BalloonAscentType()
        {
            SubActionTypes = SubActionTypes.BalloonAscent;
        }

        public string Username { get; set; } = "%User%";
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
                    HelperText = "Username displayed on the balloon ascent. Defaults to %User%."
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Emote / Character Image (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Emote code, %EmoteUrl%, image URL, or %Args%. Leave blank to fall back to the theme's default character."
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


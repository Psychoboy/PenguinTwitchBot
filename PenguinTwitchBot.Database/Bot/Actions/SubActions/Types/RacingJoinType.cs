using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Join Race / Solo Time Trial",
        description: "Enters a viewer into the active Race lobby or triggers a Solo Time Trial on the overlay",
        icon: "mdi-car-sports",
        color: "Info",
        category: SubActionCategories.Minigames,
        minigameGroup: MinigameGroups.Racing,
        tableName: "subactions_racing_join")]
    public class RacingJoinType : SubActionType, ISubActionUIProvider
    {
        public RacingJoinType()
        {
            SubActionTypes = SubActionTypes.RacingJoin;
        }

        public string Username { get; set; } = "%User%";
        public string EntryMode { get; set; } = "Auto";
        public string EmoteOrImage { get; set; } = "%Args%";
        public string ThemeOverrideId { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(Username),
                Label = "Username",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Viewer username to enter into the race. Supports variables like %User% or %TargetUser%."
            },
            new()
            {
                PropertyName = nameof(EntryMode),
                Label = "Entry Mode",
                FieldType = UIFieldType.Select,
                Required = true,
                HelperText = "Choose whether to join an open lobby, run a solo time trial, or automatically join a lobby when open and fall back to solo time trial.",
                SelectOptions =
                [
                    new SelectOption { Value = "Auto", Name = "Auto (Join Open Lobby or Solo Time Trial)" },
                    new SelectOption { Value = "Lobby", Name = "Join / Open Multiplayer Race Lobby" },
                    new SelectOption { Value = "Solo", Name = "Solo Time Trial" }
                ]
            },
            new()
            {
                PropertyName = nameof(EmoteOrImage),
                Label = "Emote / Vehicle Decal (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Optional Twitch/7TV/BTTV/FFZ emote name, image URL, or %Args% for the racer's vehicle decal."
            },
            new()
            {
                PropertyName = nameof(ThemeOverrideId),
                Label = "Theme Override",
                FieldType = UIFieldType.Select,
                AllowCustomValue = true,
                HelperText = "Optional theme override for this entry.",
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
            { nameof(Username), Username },
            { nameof(EntryMode), EntryMode },
            { nameof(EmoteOrImage), EmoteOrImage },
            { nameof(ThemeOverrideId), ThemeOverrideId },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var username)) Username = username as string ?? "%User%";
            if (values.TryGetValue(nameof(EntryMode), out var mode)) EntryMode = mode as string ?? "Auto";
            if (values.TryGetValue(nameof(EmoteOrImage), out var emote)) EmoteOrImage = emote as string ?? "";
            if (values.TryGetValue(nameof(ThemeOverrideId), out var theme)) ThemeOverrideId = theme as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var u) && string.IsNullOrWhiteSpace(u as string))
            {
                return "Username is required.";
            }
            return null;
        }
    }
}


using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Balloon Hazards",
        description: "Spawns interactive hazards (nudging droplets, slamming anvils, wind gusts, or popping darts) on the Balloon Ascent overlay",
        icon: "mdi-weather-pouring",
        color: "Primary",
        category: SubActionCategories.Minigames,
        tableName: "subactions_balloon_hazards")]
    public class BalloonHazardsType : SubActionType, ISubActionUIProvider
    {
        public BalloonHazardsType()
        {
            SubActionTypes = SubActionTypes.BalloonHazards;
        }

        public int Count { get; set; } = 6;
        public string HazardType { get; set; } = "Nudge"; // Nudge, HeavyPush, WindGust, DartPop, Random
        public string SizePreset { get; set; } = "Medium";
        public string SpeedPreset { get; set; } = "Normal";
        public string SpawnPattern { get; set; } = "TopRain";
        public string EmoteOrImage { get; set; } = "";
        public string TargetUser { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(HazardType),
                    Label = "Hazard Type",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "Nudge = playful bounce/push, HeavyPush = heavy anvil slam, WindGust = horizontal wind, DartPop = sharp pop",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Nudge", Name = "Nudge Droplet (Light push down & tilt, 0% pop)" },
                        new SelectOption { Value = "HeavyPush", Name = "Heavy Push Anvil (Heavy downward slam, 0% pop)" },
                        new SelectOption { Value = "WindGust", Name = "Wind Gust (Horizontal jetstream drift, 0% pop)" },
                        new SelectOption { Value = "DartPop", Name = "Sharp Dart (Pops balloon into free-fall)" },
                        new SelectOption { Value = "Random", Name = "Random Hazard Mix" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(Count),
                    Label = "Hazard Count",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    Max = 25,
                    HelperText = "Number of hazard projectiles to spawn (1 - 25)."
                },
                new()
                {
                    PropertyName = nameof(SpawnPattern),
                    Label = "Trajectory Pattern",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "How projectiles fall from the sky.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "TopRain", Name = "Top Rain (Falls from top across screen)" },
                        new SelectOption { Value = "Targeted", Name = "Targeted (Aimed at active balloon)" },
                        new SelectOption { Value = "SideWind", Name = "Side Wind (Streams in horizontally)" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(SpeedPreset),
                    Label = "Speed Preset",
                    FieldType = UIFieldType.Select,
                    SelectOptions =
                    [
                        new SelectOption { Value = "Normal", Name = "Normal Speed" },
                        new SelectOption { Value = "Slow", Name = "Slow" },
                        new SelectOption { Value = "Fast", Name = "Fast" },
                        new SelectOption { Value = "Hyper", Name = "Hyper" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(SizePreset),
                    Label = "Size Preset",
                    FieldType = UIFieldType.Select,
                    SelectOptions =
                    [
                        new SelectOption { Value = "Medium", Name = "Medium" },
                        new SelectOption { Value = "Small", Name = "Small" },
                        new SelectOption { Value = "Large", Name = "Large" },
                        new SelectOption { Value = "Giant", Name = "Giant" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Custom Emote / Media (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Twitch emote code, image URL, or comma-separated pool (e.g. 'sptvAnvil, PogChamp'). Leave blank for default."
                },
                new()
                {
                    PropertyName = nameof(TargetUser),
                    Label = "Target Player Username (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Specify a username to target with 'Targeted' pattern, or %User%. Leave blank to target any active balloon."
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
            { nameof(HazardType), HazardType },
            { nameof(Count), Count },
            { nameof(SpawnPattern), SpawnPattern },
            { nameof(SpeedPreset), SpeedPreset },
            { nameof(SizePreset), SizePreset },
            { nameof(EmoteOrImage), EmoteOrImage },
            { nameof(TargetUser), TargetUser },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(HazardType), out var ht)) HazardType = ht as string ?? "Nudge";
            if (values.TryGetValue(nameof(Count), out var c))
            {
                if (c is int countInt) Count = countInt;
                else if (int.TryParse(c?.ToString(), out var parsed)) Count = parsed;
            }
            if (values.TryGetValue(nameof(SpawnPattern), out var p)) SpawnPattern = p as string ?? "TopRain";
            if (values.TryGetValue(nameof(SpeedPreset), out var sp)) SpeedPreset = sp as string ?? "Normal";
            if (values.TryGetValue(nameof(SizePreset), out var sz)) SizePreset = sz as string ?? "Medium";
            if (values.TryGetValue(nameof(EmoteOrImage), out var em)) EmoteOrImage = em as string ?? "";
            if (values.TryGetValue(nameof(TargetUser), out var tu)) TargetUser = tu as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var en)) Enabled = en as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Count), out var c))
            {
                var count = c switch
                {
                    int i => i,
                    _ => int.TryParse(c?.ToString(), out var p) ? p : 5
                };
                if (count < 1 || count > 25) return "Hazard Count must be between 1 and 25.";
            }
            return null;
        }
    }
}

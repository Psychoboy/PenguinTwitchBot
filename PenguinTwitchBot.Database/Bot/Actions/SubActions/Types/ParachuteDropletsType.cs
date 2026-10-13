using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Parachute Droplets",
        description: "Spawns interactive droplets with configurable size and speed that collide with and alter the trajectory of parachuting characters",
        icon: "mdi-water",
        color: "Primary",
        category: SubActionCategories.Minigames,
        tableName: "subactions_parachute_droplets")]
    public class ParachuteDropletsType : SubActionType, ISubActionUIProvider
    {
        public ParachuteDropletsType()
        {
            SubActionTypes = SubActionTypes.ParachuteDroplets;
        }

        /// <summary>
        /// Number of droplets to spawn (1 - 30).
        /// </summary>
        public int Count { get; set; } = 5;

        /// <summary>
        /// Size preset: "Small", "Medium", "Large", "Giant", or "Random".
        /// Larger droplets have a larger hitbox and heavier knockback mass.
        /// </summary>
        public string SizePreset { get; set; } = "Medium";

        /// <summary>
        /// Speed preset: "Slow", "Normal", "Fast", "Hyper", or "Random".
        /// Faster droplets travel faster and impart greater velocity change on impact.
        /// </summary>
        public string SpeedPreset { get; set; } = "Normal";

        /// <summary>
        /// Spawn trajectory pattern: "Targeted", "TopRain", "SideWind", "Fountain".
        /// </summary>
        public string SpawnPattern { get; set; } = "Targeted";

        /// <summary>
        /// Multiplier for knockback force applied when a droplet hits a parachuting character.
        /// </summary>
        public double KnockbackMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Optional emote name, URL, or %Args% variable to use as the droplet sprite.
        /// Leave empty to use the active seasonal theme's droplet sprite.
        /// </summary>
        public string EmoteOrImage { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Count),
                    Label = "Droplet Count",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 30,
                    Required = true,
                    HelperText = "Number of droplets to launch (1 to 30)."
                },
                new()
                {
                    PropertyName = nameof(SizePreset),
                    Label = "Droplet Size",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "Larger droplets have a bigger collision radius and push parachuters further.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Small", Name = "Small (Light nudge)" },
                        new SelectOption { Value = "Medium", Name = "Medium (Standard)" },
                        new SelectOption { Value = "Large", Name = "Large (Heavy bump)" },
                        new SelectOption { Value = "Giant", Name = "Giant (Massive knockback)" },
                        new SelectOption { Value = "Random", Name = "Random Mix" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(SpeedPreset),
                    Label = "Droplet Speed",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "Faster droplets move quickly and transfer higher momentum on impact.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Slow", Name = "Slow (0.6x)" },
                        new SelectOption { Value = "Normal", Name = "Normal (1.0x)" },
                        new SelectOption { Value = "Fast", Name = "Fast (1.6x)" },
                        new SelectOption { Value = "Hyper", Name = "Hyper (2.2x)" },
                        new SelectOption { Value = "Random", Name = "Random Mix" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(SpawnPattern),
                    Label = "Spawn Pattern",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "How droplets enter the screen and intercept parachuting characters.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Targeted", Name = "Targeted (Aimed toward active parachutists)" },
                        new SelectOption { Value = "TopRain", Name = "Top Rain (Falls from sky)" },
                        new SelectOption { Value = "SideWind", Name = "Side Wind (Diagonal crosswind barrage)" },
                        new SelectOption { Value = "Fountain", Name = "Fountain (Arcs upward from sides)" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(KnockbackMultiplier),
                    Label = "Knockback Force Multiplier",
                    FieldType = UIFieldType.Float,
                    Min = 0.1f,
                    Max = 5.0f,
                    Step = 0.1f,
                    HelperText = "Scales how strongly droplets deflect parachuting characters (1.0 = normal)."
                },
                new()
                {
                    PropertyName = nameof(EmoteOrImage),
                    Label = "Custom Droplet Emote / Image (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Emote code, %Args%, or image URL. Leave blank to use the seasonal theme's droplet sprite."
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
            { nameof(Count), Count },
            { nameof(SizePreset), SizePreset },
            { nameof(SpeedPreset), SpeedPreset },
            { nameof(SpawnPattern), SpawnPattern },
            { nameof(KnockbackMultiplier), (float)KnockbackMultiplier },
            { nameof(EmoteOrImage), EmoteOrImage },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Count), out var countObj))
            {
                Count = countObj switch
                {
                    int i => Math.Clamp(i, 1, 30),
                    long l => Math.Clamp((int)l, 1, 30),
                    string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => Math.Clamp(parsed, 1, 30),
                    _ => 5
                };
            }
            if (values.TryGetValue(nameof(SizePreset), out var size)) SizePreset = size as string ?? "Medium";
            if (values.TryGetValue(nameof(SpeedPreset), out var speed)) SpeedPreset = speed as string ?? "Normal";
            if (values.TryGetValue(nameof(SpawnPattern), out var pattern)) SpawnPattern = pattern as string ?? "Targeted";
            if (values.TryGetValue(nameof(KnockbackMultiplier), out var kbObj))
            {
                KnockbackMultiplier = kbObj switch
                {
                    double d => Math.Clamp(d, 0.1, 5.0),
                    float f => Math.Clamp((double)f, 0.1, 5.0),
                    decimal m => Math.Clamp((double)m, 0.1, 5.0),
                    int i => Math.Clamp((double)i, 0.1, 5.0),
                    string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => Math.Clamp(parsed, 0.1, 5.0),
                    _ => 1.0
                };
            }
            if (values.TryGetValue(nameof(EmoteOrImage), out var emote)) EmoteOrImage = emote as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            return null;
        }
    }
}


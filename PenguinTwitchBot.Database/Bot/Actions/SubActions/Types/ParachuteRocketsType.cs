using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Parachute Sky Rockets",
        description: "Fires interactive fireworks rockets into the sky in the Parachute minigame to push parachutists or destroy their parachute on direct hit",
        icon: "mdi-rocket-launch",
        color: "Warning",
        category: SubActionCategories.Minigames,
        tableName: "subactions_parachute_rockets")]
    public class ParachuteRocketsType : SubActionType, ISubActionUIProvider
    {
        public ParachuteRocketsType()
        {
            SubActionTypes = SubActionTypes.ParachuteRockets;
        }

        /// <summary>
        /// Number of rockets to fire (1 - 10).
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Launch trajectory pattern: "Targeted", "Random", "Barrage".
        /// </summary>
        public string Pattern { get; set; } = "Targeted";

        /// <summary>
        /// Optional target username or variable template (e.g. {User}, {Target}).
        /// Leave blank to target the active parachutist.
        /// </summary>
        public string TargetUser { get; set; } = "";

        /// <summary>
        /// Percentage chance (0 to 100) that a direct canopy hit destroys the parachute.
        /// </summary>
        public int CanopyKillChance { get; set; } = 100;

        /// <summary>
        /// Multiplier for explosive knockback applied when a rocket hits or detonates near a player.
        /// </summary>
        public double KnockbackMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Crash landing scoring mode for free-falling players:
        /// "Default" (inherit from settings), "PreserveScore", "ZeroScore", or "HalfScore".
        /// </summary>
        public string CrashLandingScoreMode { get; set; } = "Default";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Count),
                    Label = "Rocket Count",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 10,
                    Required = true,
                    HelperText = "Number of rockets to launch (1 to 10)."
                },
                new()
                {
                    PropertyName = nameof(Pattern),
                    Label = "Launch Pattern",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "Pattern used to launch rockets into the sky.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Targeted", Name = "Targeted (Aims at the active or specified player)" },
                        new SelectOption { Value = "Random", Name = "Random (Unpredictable upward burst)" },
                        new SelectOption { Value = "Barrage", Name = "Barrage (Fan spread covering the sky)" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(TargetUser),
                    Label = "Target Username (Optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Username or variable like {User} or {Target}. Leave blank to target the currently falling player."
                },
                new()
                {
                    PropertyName = nameof(CanopyKillChance),
                    Label = "Canopy Kill Chance (%)",
                    FieldType = UIFieldType.Number,
                    Min = 0,
                    Max = 100,
                    Required = true,
                    HelperText = "Chance that a direct hit on the canopy pops/burns the parachute into free fall (0 - 100%)."
                },
                new()
                {
                    PropertyName = nameof(KnockbackMultiplier),
                    Label = "Knockback Force Multiplier",
                    FieldType = UIFieldType.Float,
                    Min = 0.1f,
                    Max = 5.0f,
                    Step = 0.1f,
                    HelperText = "Scales explosion deflection force (1.0 = normal)."
                },
                new()
                {
                    PropertyName = nameof(CrashLandingScoreMode),
                    Label = "Crash Landing Outcome",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    HelperText = "Scoring behavior if a player's parachute is destroyed and they crash land.",
                    SelectOptions =
                    [
                        new SelectOption { Value = "Default", Name = "Default (Use Parachute Game Setting)" },
                        new SelectOption { Value = "PreserveScore", Name = "Preserve Score (Award points based on crash landing spot)" },
                        new SelectOption { Value = "ZeroScore", Name = "Zero Score (0 points on crash landing)" },
                        new SelectOption { Value = "HalfScore", Name = "Half Score (50% penalty on crash landing)" }
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
            { nameof(Count), Count },
            { nameof(Pattern), Pattern },
            { nameof(TargetUser), TargetUser },
            { nameof(CanopyKillChance), CanopyKillChance },
            { nameof(KnockbackMultiplier), (float)KnockbackMultiplier },
            { nameof(CrashLandingScoreMode), CrashLandingScoreMode },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Count), out var countObj))
            {
                Count = countObj switch
                {
                    int i => Math.Clamp(i, 1, 10),
                    long l => Math.Clamp((int)l, 1, 10),
                    string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => Math.Clamp(parsed, 1, 10),
                    _ => 1
                };
            }
            if (values.TryGetValue(nameof(Pattern), out var p)) Pattern = p as string ?? "Targeted";
            if (values.TryGetValue(nameof(TargetUser), out var tu)) TargetUser = tu as string ?? "";
            if (values.TryGetValue(nameof(CanopyKillChance), out var ckcObj))
            {
                CanopyKillChance = ckcObj switch
                {
                    int i => Math.Clamp(i, 0, 100),
                    long l => Math.Clamp((int)l, 0, 100),
                    string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => Math.Clamp(parsed, 0, 100),
                    _ => 100
                };
            }
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
            if (values.TryGetValue(nameof(CrashLandingScoreMode), out var clsm)) CrashLandingScoreMode = clsm as string ?? "Default";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            return null;
        }
    }
}


using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Quick Tournament",
        description: "Launch a Quick / Blitz downtime fishing tournament using your multi-point payout template",
        icon: "mdi-trophy-award",
        color: "Primary",
        tableName: "subactions_fishing_quick_tournament")]
    public class FishingQuickTournamentType : SubActionType, ISubActionUIProvider
    {
        public FishingQuickTournamentType()
        {
            SubActionTypes = SubActionTypes.FishingQuickTournament;
        }

        /// <summary>Fish Category / Pool: "Random", "AllFish", or a fish category name.</summary>
        public string Theme { get; set; } = "Random";

        /// <summary>Scoring Mode: "Random", "Largest", "Smallest", "MostValuable", "MostCatches", "TotalWeight", or "Average". Stored in Text column for zero-migration compatibility.</summary>
        [NotMapped]
        public string ScoreCategory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Text)) return "Random";
                var sep = Text.IndexOf('|');
                var raw = sep >= 0 ? Text[..sep] : Text;
                return string.IsNullOrWhiteSpace(raw) ? "Random" : raw.Trim();
            }
            set
            {
                var safeScore = string.IsNullOrWhiteSpace(value) ? "Random" : value.Trim();
                var existingCustom = CustomName;
                Text = string.IsNullOrWhiteSpace(existingCustom) ? safeScore : $"{safeScore}|{existingCustom}";
            }
        }

        /// <summary>Optional custom tournament name override (leave blank to auto-name as Blitz / Derby / Tournament). Stored in Text column after '|'.</summary>
        [NotMapped]
        public string CustomName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Text)) return string.Empty;
                var sep = Text.IndexOf('|');
                return sep >= 0 && sep + 1 < Text.Length ? Text[(sep + 1)..].Trim() : string.Empty;
            }
            set
            {
                var safeScore = ScoreCategory;
                var safeCustom = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
                Text = string.IsNullOrWhiteSpace(safeCustom) ? safeScore : $"{safeScore}|{safeCustom}";
            }
        }

        public int DurationMinutes { get; set; } = 10;
        public bool PairWithFrenzy { get; set; } = true;
        public double FrenzyRarityMultiplier { get; set; } = 1.75;
        public bool AnnounceInChat { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Theme),
                    Label = "Fish Category / Pool",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    DefaultValue = "Random",
                    Options = ["Random", "AllFish"]
                },
                new()
                {
                    PropertyName = nameof(ScoreCategory),
                    Label = "Scoring Mode",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    DefaultValue = "Random",
                    Options = ["Random", "Largest", "Smallest", "MostValuable", "MostCatches", "TotalWeight", "Average"]
                },
                new()
                {
                    PropertyName = nameof(CustomName),
                    Label = "Custom Name (optional)",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "",
                    HelperText = "Leave blank to auto-name (Blitz <=15m, Derby 16-45m, Tournament >45m). Supports %variables%."
                },
                new()
                {
                    PropertyName = nameof(DurationMinutes),
                    Label = "Duration (minutes)",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 240,
                    DefaultValue = 10,
                    HelperText = "1–15m = Blitz, 16–45m = Derby, 46–240m = Tournament."
                },
                new()
                {
                    PropertyName = nameof(PairWithFrenzy),
                    Label = "Pair with Fishing Frenzy",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Warning",
                    HelperText = "Automatically activates a matching Fishing Frenzy for the duration of the tournament."
                },
                new()
                {
                    PropertyName = nameof(FrenzyRarityMultiplier),
                    Label = "Frenzy Rarity Multiplier",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "1.75"
                },
                new()
                {
                    PropertyName = nameof(AnnounceInChat),
                    Label = "Announce in Chat",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Info"
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
            [nameof(Theme)] = Theme,
            [nameof(ScoreCategory)] = ScoreCategory,
            [nameof(CustomName)] = CustomName,
            [nameof(DurationMinutes)] = DurationMinutes,
            [nameof(PairWithFrenzy)] = PairWithFrenzy,
            [nameof(FrenzyRarityMultiplier)] = FrenzyRarityMultiplier.ToString("0.##"),
            [nameof(AnnounceInChat)] = AnnounceInChat,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Theme), out var theme) && theme != null)
                Theme = theme.ToString() ?? "Random";
            if (values.TryGetValue(nameof(ScoreCategory), out var scoreCat) && scoreCat != null)
                ScoreCategory = scoreCat.ToString() ?? "Random";
            if (values.TryGetValue(nameof(CustomName), out var customName) && customName != null)
                CustomName = customName.ToString() ?? string.Empty;
            if (values.TryGetValue(nameof(DurationMinutes), out var dur) && dur != null && int.TryParse(dur.ToString(), out var parsedDur))
                DurationMinutes = Math.Clamp(parsedDur, 1, 240);
            if (values.TryGetValue(nameof(PairWithFrenzy), out var pair))
                PairWithFrenzy = pair as bool? ?? true;
            if (values.TryGetValue(nameof(FrenzyRarityMultiplier), out var mult) && mult != null && double.TryParse(mult.ToString(), out var parsedMult))
                FrenzyRarityMultiplier = Math.Clamp(parsedMult, 1.0, 10.0);
            if (values.TryGetValue(nameof(AnnounceInChat), out var announce))
                AnnounceInChat = announce as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(DurationMinutes), out var dur) && dur != null &&
                (!int.TryParse(dur.ToString(), out var parsedDur) || parsedDur < 1 || parsedDur > 240))
            {
                return "Duration must be between 1 and 240 minutes";
            }
            return null;
        }
    }
}

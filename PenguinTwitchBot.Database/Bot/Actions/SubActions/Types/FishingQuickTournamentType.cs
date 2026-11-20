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
            get => string.IsNullOrWhiteSpace(Text) ? "Random" : Text;
            set => Text = string.IsNullOrWhiteSpace(value) ? "Random" : value.Trim();
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
                    PropertyName = nameof(DurationMinutes),
                    Label = "Duration (minutes)",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 240,
                    DefaultValue = 10,
                    HelperText = "Great for stream downtime: 5, 10, or 15 minutes."
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

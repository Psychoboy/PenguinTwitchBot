using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Frenzy / Hotspot",
        description: "Start or stop a timed stream-wide Fishing Frenzy or Category Hotspot",
        icon: "mdi-fire",
        color: "Warning",
        tableName: "subactions_fishing_frenzy")]
    public class FishingFrenzyType : SubActionType, ISubActionUIProvider
    {
        public FishingFrenzyType()
        {
            SubActionTypes = SubActionTypes.FishingFrenzy;
        }

        /// <summary>"Start" or "Stop".</summary>
        public string Mode { get; set; } = "Start";
        public string FrenzyName { get; set; } = "Fishing Frenzy";
        public int DurationMinutes { get; set; } = 10;
        public double RarityMultiplier { get; set; } = 2.0;
        public string TargetCategory { get; set; } = "";
        public double CategoryBoost { get; set; } = 0.5;
        public bool AnnounceInChat { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(Mode),
                    Label = "Action",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    DefaultValue = "Start",
                    Options = ["Start", "Stop"]
                },
                new()
                {
                    PropertyName = nameof(FrenzyName),
                    Label = "Event Name",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "Fishing Frenzy",
                    HelperText = "Shown on the overlay banner and in chat (e.g., Golden Hour Frenzy, Saltwater Hotspot)."
                },
                new()
                {
                    PropertyName = nameof(DurationMinutes),
                    Label = "Duration (minutes)",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 180,
                    DefaultValue = 10
                },
                new()
                {
                    PropertyName = nameof(RarityMultiplier),
                    Label = "Rarity Multiplier",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "2.0",
                    HelperText = "Multiplier applied to non-Common rarities (e.g., 1.5 or 2.0)."
                },
                new()
                {
                    PropertyName = nameof(TargetCategory),
                    Label = "Target Category (optional)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Leave blank for all fish, or enter a category like Salt Water, Fresh Water, Junk, Mythical."
                },
                new()
                {
                    PropertyName = nameof(CategoryBoost),
                    Label = "Category Attraction (0.0 - 0.85)",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "0.5",
                    HelperText = "When Target Category is set, chance to attract fish from that category (0.5 = 50%)."
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
            [nameof(Mode)] = Mode,
            [nameof(FrenzyName)] = FrenzyName,
            [nameof(DurationMinutes)] = DurationMinutes,
            [nameof(RarityMultiplier)] = RarityMultiplier.ToString("0.##"),
            [nameof(TargetCategory)] = TargetCategory,
            [nameof(CategoryBoost)] = CategoryBoost.ToString("0.##"),
            [nameof(AnnounceInChat)] = AnnounceInChat,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Mode), out var mode) && mode != null)
                Mode = mode.ToString() ?? "Start";
            if (values.TryGetValue(nameof(FrenzyName), out var name) && name != null)
                FrenzyName = name.ToString() ?? "Fishing Frenzy";
            if (values.TryGetValue(nameof(DurationMinutes), out var dur) && dur != null && int.TryParse(dur.ToString(), out var parsedDur))
                DurationMinutes = Math.Clamp(parsedDur, 1, 180);
            if (values.TryGetValue(nameof(RarityMultiplier), out var mult) && mult != null && double.TryParse(mult.ToString(), out var parsedMult))
                RarityMultiplier = Math.Clamp(parsedMult, 1.0, 10.0);
            if (values.TryGetValue(nameof(TargetCategory), out var cat))
                TargetCategory = cat?.ToString()?.Trim() ?? "";
            if (values.TryGetValue(nameof(CategoryBoost), out var catBoost) && catBoost != null && double.TryParse(catBoost.ToString(), out var parsedCatBoost))
                CategoryBoost = Math.Clamp(parsedCatBoost, 0.0, 0.85);
            if (values.TryGetValue(nameof(AnnounceInChat), out var announce))
                AnnounceInChat = announce as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(DurationMinutes), out var dur) && dur != null &&
                (!int.TryParse(dur.ToString(), out var parsedDur) || parsedDur < 1 || parsedDur > 180))
            {
                return "Duration must be between 1 and 180 minutes";
            }
            return null;
        }
    }
}

using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Tournament Eligible Catch",
        description: "Checks whether the current fish catch matches any active tournament eligible fish and exposes match variables.",
        icon: "mdi-trophy-award",
        color: "Primary",
        tableName: "subactions_fishingtournamenteligiblecatch")]
    public class FishingTournamentEligibleCatchType : SubActionType, ISubActionUIProvider
    {
        public FishingTournamentEligibleCatchType()
        {
            SubActionTypes = SubActionTypes.FishingTournamentEligibleCatch;
        }

        public bool RequireQualifyingPosition { get; set; }

        public int QualifyingPlacementOverride { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new()
                {
                    PropertyName = "info_hint",
                    Label = "Condition",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true,
                    DefaultValue = "Checks the current catch against active tournament eligible fish and populates %fishing_tournament_eligible%, %fishing_tournament_match_count%, %fishing_tournament_ids%, %fishing_tournament_names%, and qualifying variables like %fishing_tournament_qualifying%."
                },
                new()
                {
                    PropertyName = nameof(RequireQualifyingPosition),
                    Label = "Require Qualifying Position",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary",
                    DefaultValue = false,
                    HelperText = "When enabled, only tournaments where the current user is in a reward-qualifying rank will match."
                },
                new()
                {
                    PropertyName = nameof(QualifyingPlacementOverride),
                    Label = "Qualifying Max Place Override",
                    FieldType = UIFieldType.Number,
                    Min = 0,
                    DefaultValue = 0,
                    HelperText = "0 = use tournament reward rule placements. Values above 0 override qualifying cutoff (e.g. 3 means top 3)."
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

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(RequireQualifyingPosition), RequireQualifyingPosition },
                { nameof(QualifyingPlacementOverride), QualifyingPlacementOverride },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(RequireQualifyingPosition), out var requireQualifyingPosition))
            {
                RequireQualifyingPosition = requireQualifyingPosition as bool? ?? false;
            }

            if (values.TryGetValue(nameof(QualifyingPlacementOverride), out var qualifyingPlacementOverride)
                && int.TryParse(qualifyingPlacementOverride?.ToString(), out var parsedPlacementOverride))
            {
                QualifyingPlacementOverride = Math.Max(0, parsedPlacementOverride);
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(QualifyingPlacementOverride), out var qualifyingPlacementOverride)
                && int.TryParse(qualifyingPlacementOverride?.ToString(), out var parsedPlacementOverride)
                && parsedPlacementOverride < 0)
            {
                return "Qualifying Max Place Override must be 0 or higher.";
            }

            return null;
        }
    }
}
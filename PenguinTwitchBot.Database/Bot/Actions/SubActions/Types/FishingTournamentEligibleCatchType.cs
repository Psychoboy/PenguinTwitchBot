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
                    DefaultValue = "Checks the current catch against active tournament eligible fish and populates %fishing_tournament_eligible%, %fishing_tournament_match_count%, %fishing_tournament_ids%, and %fishing_tournament_names%."
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
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            return null;
        }
    }
}
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Tournament Start",
        description: "Start a fishing tournament immediately",
        icon: "mdi-trophy",
        color: "Primary",
        tableName: "subactions_fishingtournamentstart")]
    public class FishingTournamentStartType : SubActionType, ISubActionUIProvider
    {
        public FishingTournamentStartType()
        {
            SubActionTypes = SubActionTypes.FishingTournamentStart;
        }

        public int TournamentId { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new()
                {
                    PropertyName = nameof(TournamentId),
                    Label = "Tournament ID",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    HelperText = "Fishing tournament to start immediately"
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
                { nameof(TournamentId), TournamentId },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TournamentId), out var tournamentId) && int.TryParse(tournamentId?.ToString(), out var parsedTournamentId))
            {
                TournamentId = parsedTournamentId;
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(TournamentId), out var tournamentId) ||
                !int.TryParse(tournamentId?.ToString(), out var parsedTournamentId) ||
                parsedTournamentId <= 0)
            {
                return "Tournament ID is required";
            }

            return null;
        }
    }
}
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
        public bool CloneFromTemplate { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new()
                {
                    PropertyName = nameof(CloneFromTemplate),
                    Label = "Clone from Template",
                    FieldType = UIFieldType.Switch,
                    HelperText = "When enabled, duplicates the selected tournament and starts the clone immediately.",
                    SwitchColor = "Info"
                },
                new()
                {
                    PropertyName = nameof(TournamentId),
                    Label = "Tournament / Template",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    HelperText = "Select an existing tournament to start, or a template to clone and start."
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
                { nameof(CloneFromTemplate), CloneFromTemplate },
                { nameof(TournamentId), TournamentId },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(CloneFromTemplate), out var cloneFromTemplate))
            {
                CloneFromTemplate = cloneFromTemplate as bool? ?? false;
            }

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
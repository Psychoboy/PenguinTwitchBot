using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Stop All Minigame Sessions",
        description: "Stops active timed sessions and clears queues across all overlay minigames (or a specific game)",
        icon: "mdi-timer-stop",
        color: "Warning",
        category: SubActionCategories.Minigames,
        minigameGroup: MinigameGroups.AllMinigames,
        tableName: "subactions_stop_drop_game_session")]
    public class StopDropGameSessionType : SubActionType, ISubActionUIProvider
    {
        public StopDropGameSessionType()
        {
            SubActionTypes = SubActionTypes.StopDropGameSession;
        }

        /// <summary>
        /// Minigame to stop: "All", "Parachute", "Plinko", "Balloon", "Curling", "Golf", or "Racing".
        /// </summary>
        public string GameType { get; set; } = "All";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(GameType),
                    Label = "Minigame",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new SelectOption { Value = "Parachute", Name = "Parachute Drop (!drop)" },
                        new SelectOption { Value = "Plinko", Name = "Plinko Bounce (!plinko)" },
                        new SelectOption { Value = "Balloon", Name = "Balloon Ascent (!balloon)" },
                        new SelectOption { Value = "Curling", Name = "Curling / Shuffleboard (!curl)" },
                        new SelectOption { Value = "Golf", Name = "Golf Chip & Putt (!golf)" },
                        new SelectOption { Value = "Racing", Name = "Racing (!race)" },
                        new SelectOption { Value = "All", Name = "All Minigames" }
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
            { nameof(GameType), GameType },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(GameType), out var gameType)) GameType = gameType as string ?? "All";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(GameType), out var gameType) && string.IsNullOrWhiteSpace(gameType as string))
            {
                return "Minigame selection is required.";
            }
            return null;
        }
    }
}


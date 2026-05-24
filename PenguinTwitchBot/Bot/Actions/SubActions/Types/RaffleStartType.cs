using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle Start",
        description: "Open a named raffle with configurable award and winner count",
        icon: MdiIcons.Cog,
        color: "Primary",
        tableName: "subactions_rafflestart")]
    public class RaffleStartType : SubActionType, ISubActionUIProvider
    {
        public RaffleStartType() => SubActionTypes = SubActionTypes.RaffleStart;

        public string RaffleKey { get; set; } = string.Empty;
        public string RaffleName { get; set; } = string.Empty;
        public string JoinCommand { get; set; } = string.Empty;
        public string PointGameName { get; set; } = "raffle";
        public int WinnerCount { get; set; } = 1;
        public string TotalAward { get; set; } = "0";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new() { PropertyName = nameof(RaffleKey), Label = "Raffle Key", FieldType = UIFieldType.Text, Required = true, HelperText = "Unique key used by start, enter, end, and query subactions." },
            new() { PropertyName = nameof(RaffleName), Label = "Raffle Name", FieldType = UIFieldType.Text, HelperText = "Display name written to variables for messages." },
            new() { PropertyName = nameof(JoinCommand), Label = "Join Command", FieldType = UIFieldType.Text, HelperText = "Optional command name written to variables for announcements." },
            new() { PropertyName = nameof(PointGameName), Label = "Point Game", FieldType = UIFieldType.Text, Required = true, DefaultValue = "raffle", HelperText = "Point game that winners will be awarded when the raffle ends." },
            new() { PropertyName = nameof(WinnerCount), Label = "Winner Count", FieldType = UIFieldType.Number, Required = true, DefaultValue = 1, Min = 1 },
            new() { PropertyName = nameof(TotalAward), Label = "Total Award", FieldType = UIFieldType.Text, Required = true, DefaultValue = "0", HelperText = "You can enter a number or a %variable%." },
            new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success", DefaultValue = true }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            [nameof(RaffleKey)] = RaffleKey,
            [nameof(RaffleName)] = RaffleName,
            [nameof(JoinCommand)] = JoinCommand,
            [nameof(PointGameName)] = PointGameName,
            [nameof(WinnerCount)] = WinnerCount,
            [nameof(TotalAward)] = TotalAward,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            RaffleKey = values.GetValueOrDefault(nameof(RaffleKey))?.ToString() ?? string.Empty;
            RaffleName = values.GetValueOrDefault(nameof(RaffleName))?.ToString() ?? string.Empty;
            JoinCommand = values.GetValueOrDefault(nameof(JoinCommand))?.ToString() ?? string.Empty;
            PointGameName = values.GetValueOrDefault(nameof(PointGameName))?.ToString() ?? "raffle";
            WinnerCount = TryParseInt(values.GetValueOrDefault(nameof(WinnerCount)), 1);
            TotalAward = values.GetValueOrDefault(nameof(TotalAward))?.ToString() ?? "0";
            Enabled = TryParseBool(values.GetValueOrDefault(nameof(Enabled)), true);
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(RaffleKey))?.ToString()))
            {
                return "Raffle Key is required";
            }

            if (TryParseInt(values.GetValueOrDefault(nameof(WinnerCount)), 0) < 1)
            {
                return "Winner Count must be at least 1";
            }

            var totalAward = values.GetValueOrDefault(nameof(TotalAward))?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(totalAward))
            {
                return "Total Award is required";
            }

            if (totalAward.Contains(' '))
            {
                return "Total Award cannot contain spaces";
            }

            return null;
        }

        private static int TryParseInt(object? value, int fallback) => int.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
        private static bool TryParseBool(object? value, bool fallback) => bool.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
    }
}
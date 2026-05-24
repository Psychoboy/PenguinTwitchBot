using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle Set Winner Count",
        description: "Update the winner count for a running raffle",
        icon: MdiIcons.Cog,
        color: "Primary",
        tableName: "subactions_rafflesetwinnercount")]
    public class RaffleSetWinnerCountType : SubActionType, ISubActionUIProvider
    {
        public RaffleSetWinnerCountType() => SubActionTypes = SubActionTypes.RaffleSetWinnerCount;

        public string RaffleKey { get; set; } = string.Empty;
        public int WinnerCount { get; set; } = 1;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new() { PropertyName = nameof(RaffleKey), Label = "Raffle Key", FieldType = UIFieldType.Text, Required = true },
            new() { PropertyName = nameof(WinnerCount), Label = "Winner Count", FieldType = UIFieldType.Number, Required = true, DefaultValue = 1, Min = 1 },
            new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success", DefaultValue = true }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            [nameof(RaffleKey)] = RaffleKey,
            [nameof(WinnerCount)] = WinnerCount,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            RaffleKey = values.GetValueOrDefault(nameof(RaffleKey))?.ToString() ?? string.Empty;
            WinnerCount = int.TryParse(values.GetValueOrDefault(nameof(WinnerCount))?.ToString(), out var winnerCount) ? winnerCount : 1;
            Enabled = bool.TryParse(values.GetValueOrDefault(nameof(Enabled))?.ToString(), out var enabled) ? enabled : true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(RaffleKey))?.ToString()))
            {
                return "Raffle Key is required";
            }

            return int.TryParse(values.GetValueOrDefault(nameof(WinnerCount))?.ToString(), out var winnerCount) && winnerCount > 0
                ? null
                : "Winner Count must be at least 1";
        }
    }
}
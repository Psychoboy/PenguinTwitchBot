using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Raffle Set Total Award",
        description: "Update the total award for a running raffle",
        icon: "mdi-cog",
        color: "Primary",
        tableName: "subactions_rafflesettotalaward")]
    public class RaffleSetTotalAwardType : SubActionType, ISubActionUIProvider
    {
        public RaffleSetTotalAwardType() => SubActionTypes = SubActionTypes.RaffleSetTotalAward;

        public string RaffleKey { get; set; } = string.Empty;
        public string TotalAward { get; set; } = "0";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new() { PropertyName = nameof(RaffleKey), Label = "Raffle Key", FieldType = UIFieldType.Text, Required = true },
            new() { PropertyName = nameof(TotalAward), Label = "Total Award", FieldType = UIFieldType.Text, Required = true, DefaultValue = "0", HelperText = "You can enter a number or a %variable%." },
            new() { PropertyName = nameof(Enabled), Label = "Enabled", FieldType = UIFieldType.Switch, SwitchColor = "Success", DefaultValue = true }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            [nameof(RaffleKey)] = RaffleKey,
            [nameof(TotalAward)] = TotalAward,
            [nameof(Enabled)] = Enabled
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            RaffleKey = values.GetValueOrDefault(nameof(RaffleKey))?.ToString() ?? string.Empty;
            TotalAward = values.GetValueOrDefault(nameof(TotalAward))?.ToString() ?? "0";
            Enabled = bool.TryParse(values.GetValueOrDefault(nameof(Enabled))?.ToString(), out var enabled) ? enabled : true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(nameof(RaffleKey))?.ToString()))
            {
                return "Raffle Key is required";
            }

            var totalAward = values.GetValueOrDefault(nameof(TotalAward))?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(totalAward))
            {
                return "Total Award is required";
            }

            return totalAward.Contains(' ') ? "Total Award cannot contain spaces" : null;
        }
    }
}
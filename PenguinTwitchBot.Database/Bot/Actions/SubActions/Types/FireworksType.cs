using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fireworks",
        description: "Launches fireworks on the Fireworks overlay widget; requires that widget in an overlay layout",
        icon: "mdi-firework",
        color: "Warning",
        tableName: "subactions_fireworks",
        category: SubActionCategories.ChatAndMedia)]
    public class FireworksType : SubActionType, ISubActionUIProvider
    {
        public FireworksType() => SubActionTypes = SubActionTypes.Fireworks;

        public string Count { get; set; } = "1";
        public string DelayMilliseconds { get; set; } = "100";
        public string Shell { get; set; } = "";
        public string Size { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(Count),
                Label = "Burst Count",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Number of bursts (1-100). Supports %variables% and $math(expression)$; for example, $math(%Amount% + 1)$."
            },
            new()
            {
                PropertyName = nameof(DelayMilliseconds),
                Label = "Delay Between Bursts (ms)",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Delay between bursts in milliseconds (0-60000). Supports %variables% and $math(expression)$."
            },
            new()
            {
                PropertyName = nameof(Shell),
                Label = "Shell Override (Optional)",
                FieldType = UIFieldType.Text,
                HelperText = "Leave blank to use the Fireworks widget setting. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(Size),
                Label = "Size Override (Optional)",
                FieldType = UIFieldType.Text,
                HelperText = "Leave blank to use the Fireworks widget setting. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(Enabled),
                Label = "Enabled",
                FieldType = UIFieldType.Switch,
                SwitchColor = "Success"
            }
        ];

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(Count), Count },
            { nameof(DelayMilliseconds), DelayMilliseconds },
            { nameof(Shell), Shell },
            { nameof(Size), Size },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Count), out var count)) Count = count as string ?? "1";
            if (values.TryGetValue(nameof(DelayMilliseconds), out var delay)) DelayMilliseconds = delay as string ?? "100";
            if (values.TryGetValue(nameof(Shell), out var shell)) Shell = shell as string ?? "";
            if (values.TryGetValue(nameof(Size), out var size)) Size = size as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Credits - Mark Start Checkpoint",
        description: "Marks a manual start checkpoint timestamp for credits time windows",
        icon: "mdi-flag-checkered",
        color: "Warning",
        tableName: "subactions_credits_mark_start",
        category: SubActionCategories.Credits)]
    public class CreditsMarkStartType : SubActionType, ISubActionUIProvider
    {
        public CreditsMarkStartType()
        {
            SubActionTypes = SubActionTypes.CreditsMarkStart;
        }

        public string ProfileName { get; set; } = "";

        public string Label { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(ProfileName),
                Label = "Credits Profile (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Leave blank to set a global checkpoint and update all checkpoint profiles, or specify a profile name."
            },
            new()
            {
                PropertyName = nameof(Label),
                Label = "Checkpoint Label (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Optional label (e.g., 'Subathon Day 2'). Supports %variables%."
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
            { nameof(ProfileName), ProfileName },
            { nameof(Label), Label },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ProfileName), out var profile))
                ProfileName = (profile as string)?.Trim() ?? "";
            if (values.TryGetValue(nameof(Label), out var label))
                Label = (label as string)?.Trim() ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}

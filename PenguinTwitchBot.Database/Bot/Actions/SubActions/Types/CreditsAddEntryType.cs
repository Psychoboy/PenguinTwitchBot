using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Credits - Add Manual Entry",
        description: "Records a custom viewer or cast entry for the Action-Added Entries credits section",
        icon: "mdi-account-plus",
        color: "Primary",
        tableName: "subactions_credits_add_entry",
        category: SubActionCategories.Credits)]
    public class CreditsAddEntryType : SubActionType, ISubActionUIProvider
    {
        public CreditsAddEntryType()
        {
            SubActionTypes = SubActionTypes.CreditsAddEntry;
        }

        public string Username { get; set; } = "%user%";

        public string DisplayName { get; set; } = "%displayName%";

        public string SectionTag { get; set; } = "Manual";

        public string Amount { get; set; } = "1";

        public string Detail { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(Username),
                Label = "Username",
                FieldType = UIFieldType.Text,
                Required = true,
                HelperText = "Twitch login or identifier. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(DisplayName),
                Label = "Display Name",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Display name shown in credits. Defaults to Username if blank. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(SectionTag),
                Label = "Section Tag",
                FieldType = UIFieldType.Text,
                Required = false,
                DefaultValue = "Manual",
                HelperText = "Optional tag to filter into specific 'Action-Added Entries' sections via Detail Filter. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(Amount),
                Label = "Amount",
                FieldType = UIFieldType.Text,
                Required = false,
                DefaultValue = "1",
                HelperText = "Numeric amount (for {amount} formatting and sorting). Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(Detail),
                Label = "Detail / Role (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Role or detail text (for {detail} formatting). Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(AvatarUrl),
                Label = "Avatar URL (Optional)",
                FieldType = UIFieldType.Text,
                Required = false,
                HelperText = "Leave blank to auto-resolve Twitch avatar from Username. Supports %variables%."
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
            { nameof(Username), Username },
            { nameof(DisplayName), DisplayName },
            { nameof(SectionTag), SectionTag },
            { nameof(Amount), Amount },
            { nameof(Detail), Detail },
            { nameof(AvatarUrl), AvatarUrl },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Username), out var username)) Username = username as string ?? "%user%";
            if (values.TryGetValue(nameof(DisplayName), out var displayName)) DisplayName = displayName as string ?? "";
            if (values.TryGetValue(nameof(SectionTag), out var tag)) SectionTag = string.IsNullOrWhiteSpace(tag as string) ? "Manual" : (tag as string)!.Trim();
            if (values.TryGetValue(nameof(Amount), out var amount)) Amount = string.IsNullOrWhiteSpace(amount as string) ? "1" : (amount as string)!.Trim();
            if (values.TryGetValue(nameof(Detail), out var detail)) Detail = detail as string ?? "";
            if (values.TryGetValue(nameof(AvatarUrl), out var avatar)) AvatarUrl = avatar as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled)) Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Username), out var u) || string.IsNullOrWhiteSpace(u as string))
                return "Username is required.";
            return null;
        }
    }
}

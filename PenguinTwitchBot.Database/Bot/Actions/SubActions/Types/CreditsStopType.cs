using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Credits - Stop / Hide",
        description: "Stops and hides the Stream Credits overlay",
        icon: "mdi-stop-circle-outline",
        color: "Error",
        tableName: "subactions_credits_stop",
        category: SubActionCategories.Credits)]
    public class CreditsStopType : SubActionType, ISubActionUIProvider
    {
        public CreditsStopType()
        {
            SubActionTypes = SubActionTypes.CreditsStop;
        }

        public string ProfileName { get; set; } = "Default";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(ProfileName),
                Label = "Credits Profile",
                FieldType = UIFieldType.Text,
                Required = false,
                DefaultValue = "Default",
                HelperText = "Profile name to stop (leave empty for 'Default', or '*' for all). Supports %variables%."
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
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ProfileName), out var profile))
                ProfileName = string.IsNullOrWhiteSpace(profile as string) ? "Default" : (profile as string)!.Trim();
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}

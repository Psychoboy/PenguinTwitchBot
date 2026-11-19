using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Credits - Pause / Resume",
        description: "Pauses, resumes, toggles, or restarts the rolling Stream Credits overlay",
        icon: "mdi-play-pause",
        color: "Info",
        tableName: "subactions_credits_pause_resume",
        category: SubActionCategories.Credits)]
    public class CreditsPauseResumeType : SubActionType, ISubActionUIProvider
    {
        public const string Toggle = "toggle";
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Restart = "restart";

        public CreditsPauseResumeType()
        {
            SubActionTypes = SubActionTypes.CreditsPauseResume;
        }

        public string ProfileName { get; set; } = "Default";

        public string ActionMode { get; set; } = Toggle;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null) =>
        [
            new()
            {
                PropertyName = nameof(ProfileName),
                Label = "Credits Profile",
                FieldType = UIFieldType.Text,
                Required = false,
                DefaultValue = "Default",
                HelperText = "Profile name to control. Supports %variables%."
            },
            new()
            {
                PropertyName = nameof(ActionMode),
                Label = "Playback Action",
                FieldType = UIFieldType.Select,
                Required = true,
                SelectOptions =
                [
                    new SelectOption { Value = Toggle, Name = "Toggle Pause / Resume" },
                    new SelectOption { Value = Pause, Name = "Pause" },
                    new SelectOption { Value = Resume, Name = "Resume" },
                    new SelectOption { Value = Restart, Name = "Restart from Beginning" }
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

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(ProfileName), ProfileName },
            { nameof(ActionMode), ActionMode },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ProfileName), out var profile))
                ProfileName = string.IsNullOrWhiteSpace(profile as string) ? "Default" : (profile as string)!.Trim();
            if (values.TryGetValue(nameof(ActionMode), out var mode))
                ActionMode = string.IsNullOrWhiteSpace(mode as string) ? Toggle : (mode as string)!.Trim();
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}

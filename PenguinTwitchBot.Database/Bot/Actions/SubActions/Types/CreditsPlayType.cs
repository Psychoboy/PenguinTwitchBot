using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Credits - Play / Roll",
        description: "Starts rolling the Stream Credits overlay for the selected profile",
        icon: "mdi-movie-play",
        color: "Success",
        tableName: "subactions_credits_play",
        category: SubActionCategories.Credits)]
    public class CreditsPlayType : SubActionType, ISubActionUIProvider
    {
        public CreditsPlayType()
        {
            SubActionTypes = SubActionTypes.CreditsPlay;
        }

        public string ProfileName { get; set; } = "Default";

        public string WindowOverrideMode { get; set; } = "";

        public string LookbackMinutes { get; set; } = "";

        public string SampleMode { get; set; } = CreditSampleModes.Off;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(ProfileName),
                    Label = "Credits Profile",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    DefaultValue = "Default",
                    HelperText = "Profile name to roll. Supports %variables%. Leave blank for 'Default'."
                },
                new()
                {
                    PropertyName = nameof(WindowOverrideMode),
                    Label = "Time Window Override",
                    FieldType = UIFieldType.Select,
                    Required = false,
                    SelectOptions =
                    [
                        new SelectOption { Value = "", Name = "Use Profile Default" },
                        new SelectOption { Value = CreditTimeWindowModes.SinceStreamStart, Name = "Since Current Stream Start" },
                        new SelectOption { Value = CreditTimeWindowModes.LastCompletedStream, Name = "Last Completed Stream" },
                        new SelectOption { Value = CreditTimeWindowModes.RollingLookback, Name = "Rolling Lookback (Minutes)" },
                        new SelectOption { Value = CreditTimeWindowModes.SinceManualCheckpoint, Name = "Since Manual Checkpoint" }
                    ]
                },
                new()
                {
                    PropertyName = nameof(LookbackMinutes),
                    Label = "Lookback Minutes (Optional)",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    HelperText = "Used when Time Window Override is Rolling Lookback. Supports %variables%."
                },
                new()
                {
                    PropertyName = nameof(SampleMode),
                    Label = "Data Mode",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new SelectOption { Value = CreditSampleModes.Off, Name = "Live Events" },
                        new SelectOption { Value = CreditSampleModes.Fake, Name = "Test Mode (Fake Sample Data)" },
                        new SelectOption { Value = CreditSampleModes.DatabaseViewers, Name = "Test Mode (Viewers from DB)" }
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
            { nameof(ProfileName), ProfileName },
            { nameof(WindowOverrideMode), WindowOverrideMode },
            { nameof(LookbackMinutes), LookbackMinutes },
            { nameof(SampleMode), SampleMode },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ProfileName), out var profile))
                ProfileName = string.IsNullOrWhiteSpace(profile as string) ? "Default" : (profile as string)!.Trim();
            if (values.TryGetValue(nameof(WindowOverrideMode), out var windowMode))
                WindowOverrideMode = windowMode as string ?? "";
            if (values.TryGetValue(nameof(LookbackMinutes), out var lookback))
                LookbackMinutes = lookback as string ?? "";
            if (values.TryGetValue(nameof(SampleMode), out var sample))
                SampleMode = string.IsNullOrWhiteSpace(sample as string) ? CreditSampleModes.Off : (sample as string)!.Trim();
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(WindowOverrideMode), out var wm) &&
                string.Equals(wm as string, CreditTimeWindowModes.RollingLookback, StringComparison.OrdinalIgnoreCase) &&
                values.TryGetValue(nameof(LookbackMinutes), out var lb))
            {
                var raw = lb as string ?? "";
                if (!raw.Contains('%') && (!int.TryParse(raw, out var mins) || mins <= 0))
                    return "Lookback minutes must be a positive whole number when Rolling Lookback override is selected.";
            }

            return null;
        }
    }
}

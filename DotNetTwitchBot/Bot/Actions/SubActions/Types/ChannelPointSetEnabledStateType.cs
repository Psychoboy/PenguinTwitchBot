using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Set Channel Point Enabled State",
        description: "Enable or disable a channel point reward",
        icon: MdiIcons.ToggleSwitch,
        color: "Primary",
        tableName: "subactions_channelpointsetenabledstate")]
    public class ChannelPointSetEnabledStateType : SubActionType, ISubActionUIProvider
    {
        public ChannelPointSetEnabledStateType()
        {
            SubActionTypes = SubActionTypes.ChannelPointSetEnabledState;
        }
        public bool EnablePoint { get; set; } = true;
        public List<SubActionUIField> GetUIFields()
        {
            return new()
            {
                new SubActionUIField
                {
                    PropertyName = nameof(Text),
                    Label = "Reward Name",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The name of the channel point reward to enable or disable"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(EnablePoint),
                    Label = "Enable Point?",
                    FieldType = UIFieldType.Switch,
                    Required = true,
                    HelperText = "Whether to enable or disable the channel point reward"
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(EnablePoint), EnablePoint },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if(values.TryGetValue(nameof(EnablePoint), out var enablePoint))
                EnablePoint = enablePoint as bool? ?? true;
            if(values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Channelpoint Name is required";
            return null;
        }
    }
}

using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Set Channel Point Paused State",
        description: "Pause or unpause channel point redemptions",
        icon: "mdi-pause-circle",
        color: "Warning",
        tableName: "subactions_channelpointsetpausedstate")]
    public class ChannelPointSetPausedStateType : SubActionType, ISubActionUIProvider
    {
        public bool IsPaused { get; set; }
        public ChannelPointSetPausedStateType()
        {
            SubActionTypes = SubActionTypes.ChannelPointSetPausedState;
        }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
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
                    PropertyName = nameof(IsPaused),
                    Label = "Pause Point?",
                    FieldType = UIFieldType.Switch,
                    Required = true,
                    HelperText = "Whether to pause or unpause the channel point reward"
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
                { nameof(IsPaused), IsPaused },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if(values.TryGetValue(nameof(IsPaused), out var isPaused))
                IsPaused = isPaused as bool? ?? true;
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

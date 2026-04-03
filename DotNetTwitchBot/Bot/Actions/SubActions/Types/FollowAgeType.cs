using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Follow Age",
        description: "Get how long a user has been following",
        icon: MdiIcons.Heart,
        color: "Default",
        tableName: "subactions_followage")]
    public class FollowAgeType : SubActionType, ISubActionUIProvider
    {
        public FollowAgeType()
        {
            SubActionTypes = SubActionTypes.Followage;
        }

        public new string Text { get; set; } = "%targetorself%";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Target user (or use %targetorself%)",
                    FieldType = UIFieldType.Text,
                    HelperText = "Use variables like %user%, %target%, %targetorself%"
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
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "%targetorself%";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values) => null;
    }
}

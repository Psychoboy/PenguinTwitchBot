using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Send Message",
        description: "Send a chat message to Twitch",
        icon: MdiIcons.MessageText,
        color: "Primary",
        tableName: "subactions_sendmessage")]
    public class SendMessageType : ChatType, ISubActionUIProvider
    {
        public SendMessageType()
        {
            SubActionTypes = SubActionTypes.SendMessage;
        }

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Message",
                    FieldType = UIFieldType.TextArea,
                    Required = true,
                    HelperText = "Use variables like %user%, %target%, %random.1-100%, etc.",
                    Lines = 3
                },
                new()
                {
                    PropertyName = nameof(UseBot),
                    Label = "Use Bot Account",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary"
                },
                new()
                {
                    PropertyName = nameof(FallBack),
                    Label = "Fall Back to Broadcaster",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary"
                },
                new()
                {
                    PropertyName = nameof(StreamOnly),
                    Label = "Stream Only",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary"
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
                { nameof(UseBot), UseBot },
                { nameof(FallBack), FallBack },
                { nameof(StreamOnly), StreamOnly },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if (values.TryGetValue(nameof(UseBot), out var useBot))
                UseBot = useBot as bool? ?? true;
            if (values.TryGetValue(nameof(FallBack), out var fallBack))
                FallBack = fallBack as bool? ?? true;
            if (values.TryGetValue(nameof(StreamOnly), out var streamOnly))
                StreamOnly = streamOnly as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Message is required";
            return null;
        }
    }
}

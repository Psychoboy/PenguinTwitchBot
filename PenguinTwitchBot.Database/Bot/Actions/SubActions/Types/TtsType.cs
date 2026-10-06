using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "TTS",
        description: "Text to speech using Googles's TTS system",
        icon: "mdi-account-voice",
        color: "Primary",
        tableName: "subactions_tts")]
    public class TtsType : SubActionType, ISubActionUIProvider
    {
        public TtsType() { SubActionTypes = SubActionTypes.Tts; }
        public string? Name { get; set; }
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new()
            {
                new()
                {
                    PropertyName = nameof(Name),
                    Label = "Username to get voice from",
                    FieldType = UIFieldType.Text,
                    Required = false,
                    HelperText = "Who to get voice from, if empty or not found will get random voice. Supports variables like %user%."
                },
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Message",
                    FieldType = UIFieldType.TextArea,
                    Required = true,
                    HelperText = "The message that will be spoken by the TTS system. Supports variables like %user%, %target%, %message%.",
                    Lines = 3,
                    Resizable = true
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
                { nameof(Name), Name },
                { nameof(Text), Text },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Name), out var name))
                Name = name as string;
            if(values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if(values.TryGetValue(nameof(Enabled), out var enabled) && enabled is bool)
                Enabled = (bool)enabled;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Message is required";
            return null;
        }
    }
}

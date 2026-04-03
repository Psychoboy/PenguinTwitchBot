using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Play Sound",
        description: "Play an audio file",
        icon: "mdi-volume-high",
        color: "Info",
        tableName: "subactions_playsound")]
    public class PlaySoundType : SubActionType, ISubActionUIProvider
    {
        public PlaySoundType()
        {
            SubActionTypes = SubActionTypes.PlaySound;
        }

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(File),
                    Label = "Sound File",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Path to audio file"
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    Attributes = new Dictionary<string, object> { { "Color", "Success" } }
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(File), File },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(File), out var file))
                File = file as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(File), out var file) || string.IsNullOrWhiteSpace(file as string))
                return "Sound file is required";
            return null;
        }
    }
}

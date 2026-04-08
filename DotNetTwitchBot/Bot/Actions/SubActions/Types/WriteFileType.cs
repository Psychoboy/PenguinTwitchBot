using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Write File",
        description: "Write or append text to a file",
        icon: MdiIcons.ContentSave,
        color: "Success",
        tableName: "subactions_writefile")]
    public class WriteFileType : SubActionType, ISubActionUIProvider
    {
        public WriteFileType()
        {
            SubActionTypes = SubActionTypes.WriteFile;
        }

        public bool Append { get; set; } = true;
        public string File { get; set; } = "";

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(File),
                    Label = "File Path",
                    FieldType = UIFieldType.Text,
                    Required = true
                },
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Content",
                    FieldType = UIFieldType.TextArea,
                    Required = true,
                    Lines = 3
                },
                new()
                {
                    PropertyName = nameof(Append),
                    Label = "Append to File",
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
                { nameof(File), File },
                { nameof(Text), Text },
                { nameof(Append), Append },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(File), out var file))
                File = file as string ?? "";
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if (values.TryGetValue(nameof(Append), out var append))
                Append = append as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(File), out var file) || string.IsNullOrWhiteSpace(file as string))
                return "File path is required";
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Content is required";
            return null;
        }
    }
}

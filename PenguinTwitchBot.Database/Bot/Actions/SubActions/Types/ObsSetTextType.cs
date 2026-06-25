using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Text",
        description: "Update the text content of a text source in OBS",
        icon: "mdi-text",
        color: "Warning",
        tableName: "subactions_obs_settext")]
    public class ObsSetTextType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] TextInputKinds = ["text_gdiplus_v3", "text_gdiplus_v2", "text_gdiplus", "text_ft2_source_v2", "text_ft2_source"];

        public ObsSetTextType()
        {
            SubActionTypes = SubActionTypes.ObsSetText;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string TextContent { get; set; } = string.Empty;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new SubActionUIField
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
                { nameof(OBSConnectionId), OBSConnectionId },
                { nameof(InputName), InputName },
                { nameof(TextContent), TextContent },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(OBSConnectionId), out var connectionId))
            {
                if (connectionId is string strId && int.TryParse(strId, out var id))
                    OBSConnectionId = id;
                else if (connectionId is int intId)
                    OBSConnectionId = intId;
            }

            if (values.TryGetValue(nameof(InputName), out var inputName))
                InputName = inputName as string ?? "";

            if (values.TryGetValue(nameof(TextContent), out var textContent))
                TextContent = textContent as string ?? "";

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(InputName), out var inputName) || string.IsNullOrWhiteSpace(inputName as string))
                return "Text Source Name is required";

            if (!values.TryGetValue(nameof(TextContent), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Text content is required";

            return null;
        }
    }
}

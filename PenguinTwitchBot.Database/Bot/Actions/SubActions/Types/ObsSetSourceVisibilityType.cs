using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Visibility",
        description: "Show or hide a source in a scene",
        icon: "mdi-eye",
        color: "Warning",
        tableName: "subactions_obs_setsourcevisibility")]
    public class ObsSetSourceVisibilityType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSourceVisibilityType()
        {
            SubActionTypes = SubActionTypes.ObsSetSourceVisibility;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string SourceName { get; set; } = string.Empty;

        public bool Visible { get; set; } = true;

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
                { nameof(SceneName), SceneName },
                { nameof(SourceName), SourceName },
                { nameof(Visible), Visible },
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

            if (values.TryGetValue(nameof(SceneName), out var scene))
                SceneName = scene as string ?? "";

            if (values.TryGetValue(nameof(SourceName), out var source))
                SourceName = source as string ?? "";

            if (values.TryGetValue(nameof(Visible), out var visible))
                Visible = visible as bool? ?? true;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            if (!values.TryGetValue(nameof(SourceName), out var source) || string.IsNullOrWhiteSpace(source as string))
                return "Source Name is required";

            return null;
        }
    }
}

using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Scene",
        description: "Change the current scene in OBS",
        icon: "mdi-video",
        color: "Warning",
        tableName: "subactions_obs_setscene")]
    public class ObsSetSceneType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSceneType()
        {
            SubActionTypes = SubActionTypes.ObsSetScene;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

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
            {
                // Scene name can come as the scene name string directly when using dropdown
                SceneName = scene as string ?? "";
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            return null;
        }
    }
}

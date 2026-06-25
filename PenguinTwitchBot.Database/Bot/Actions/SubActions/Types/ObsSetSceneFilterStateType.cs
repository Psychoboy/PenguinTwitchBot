using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Scene Filter State",
        description: "Enable or disable a filter on an OBS scene",
        icon: "mdi-filter-variant",
        color: "Warning",
        tableName: "subactions_obs_setscenefilterstate")]
    public class ObsSetSceneFilterStateType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSceneFilterStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetSceneFilterState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string FilterName { get; set; } = string.Empty;

        public bool FilterEnabled { get; set; } = true;

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
                { nameof(FilterName), FilterName },
                { nameof(FilterEnabled), FilterEnabled },
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

            if (values.TryGetValue(nameof(FilterName), out var filter))
                FilterName = filter as string ?? "";

            if (values.TryGetValue(nameof(FilterEnabled), out var filterEnabled))
                FilterEnabled = filterEnabled as bool? ?? true;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            if (!values.TryGetValue(nameof(FilterName), out var filter) || string.IsNullOrWhiteSpace(filter as string))
                return "Filter Name is required";

            return null;
        }
    }
}

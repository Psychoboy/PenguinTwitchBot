using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Mute State",
        description: "Mute or unmute a source in OBS",
        icon: "mdi-microphone",
        color: "Warning",
        tableName: "subactions_obs_setinputmute")]
    public class ObsSetSourceMuteStateType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSourceMuteStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetSourceMuteState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        public bool Muted { get; set; } = false;

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
                { nameof(Muted), Muted },
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

            if (values.TryGetValue(nameof(Muted), out var muted))
                Muted = muted as bool? ?? false;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(InputName), out var inputName) || string.IsNullOrWhiteSpace(inputName as string))
                return "Input Name is required";

            return null;
        }
    }
}

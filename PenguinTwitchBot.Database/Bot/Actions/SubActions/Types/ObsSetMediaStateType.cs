using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Media State",
        description: "Play, pause, stop, or restart a media source in OBS",
        icon: "mdi-play-circle",
        color: "Warning",
        tableName: "subactions_obs_setmediastate")]
    public class ObsSetMediaStateType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] MediaInputKinds = ["ffmpeg_source", "vlc_source"];

        public static readonly string[] MediaActions =
        [
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS"
        ];

        public static readonly string[] MediaActionLabels =
        [
            "Play",
            "Pause",
            "Stop",
            "Restart",
            "Next",
            "Previous"
        ];

        public ObsSetMediaStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetMediaState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string MediaAction { get; set; } = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY";

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

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(OBSConnectionId), OBSConnectionId },
            { nameof(InputName), InputName },
            { nameof(MediaAction), MediaAction },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(OBSConnectionId), out var connId))
            {
                if (connId is string s && int.TryParse(s, out var i)) OBSConnectionId = i;
                else if (connId is int intId) OBSConnectionId = intId;
            }
            if (values.TryGetValue(nameof(InputName), out var n)) InputName = n as string ?? "";
            if (values.TryGetValue(nameof(MediaAction), out var a)) MediaAction = a as string ?? MediaActions[0];
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";
            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Media Source Name is required";
            if (!values.TryGetValue(nameof(MediaAction), out var a) || string.IsNullOrWhiteSpace(a as string))
                return "Action is required";
            return null;
        }
    }
}

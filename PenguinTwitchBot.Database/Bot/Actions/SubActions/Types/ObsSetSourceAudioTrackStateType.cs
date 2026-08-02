using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Audio Track State",
        description: "Enable or disable an audio mix track for a source in OBS",
        icon: "mdi-volume-high",
        color: "Warning",
        tableName: "subactions_obs_setsourceaudiotrackstate")]
    public class ObsSetSourceAudioTrackStateType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSourceAudioTrackStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetSourceAudioTrackState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        /// <summary>
        /// Audio track number (1–6).
        /// </summary>
        public int TrackNumber { get; set; } = 1;

        public bool TrackEnabled { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new SubActionUIField
                {
                    PropertyName = nameof(OBSConnectionId),
                    Label = "OBS Connection",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    HelperText = "Select your OBS connection"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(InputName),
                    Label = "Source Name",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Audio source name"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(TrackNumber),
                    Label = "Track Number",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    Max = 6,
                    Step = 1,
                    HelperText = "Audio track from 1 to 6"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(TrackEnabled),
                    Label = "Track Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary"
                },
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
            { nameof(TrackNumber), TrackNumber },
            { nameof(TrackEnabled), TrackEnabled },
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
            if (values.TryGetValue(nameof(TrackNumber), out var t))
            {
                if (t is string ts && int.TryParse(ts, out var ti)) TrackNumber = ti;
                else if (t is int ti2) TrackNumber = ti2;
            }
            if (values.TryGetValue(nameof(TrackEnabled), out var te)) TrackEnabled = te as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            var parsedConnectionId = connId switch
            {
                string s when int.TryParse(s, out var parsed) => parsed,
                int i => i,
                _ => 0
            };

            if (parsedConnectionId < 1)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Source Name is required";
            if (!values.TryGetValue(nameof(TrackNumber), out var t) || t == null)
                return "Track Number is required";

            int? parsedTrackNumber = t switch
            {
                string s when int.TryParse(s, out var parsed) => parsed,
                int i => i,
                _ => null
            };

            if (parsedTrackNumber is null || parsedTrackNumber < 1 || parsedTrackNumber > 6)
                return "Track Number must be between 1 and 6";

            return null;
        }
    }
}

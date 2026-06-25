using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Media Source File",
        description: "Change the file or URL played by a media source in OBS",
        icon: "mdi-video",
        color: "Warning",
        tableName: "subactions_obs_setmediasourcefile")]
    public class ObsSetMediaSourceFileType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] MediaInputKinds = ["ffmpeg_source", "vlc_source"];

        public ObsSetMediaSourceFileType()
        {
            SubActionTypes = SubActionTypes.ObsSetMediaSourceFile;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string FilePath { get; set; } = string.Empty;

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
            { nameof(FilePath), FilePath },
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
            if (values.TryGetValue(nameof(FilePath), out var f)) FilePath = f as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";
            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Media Source Name is required";
            if (!values.TryGetValue(nameof(FilePath), out var f) || string.IsNullOrWhiteSpace(f as string))
                return "File Path is required";
            return null;
        }
    }
}

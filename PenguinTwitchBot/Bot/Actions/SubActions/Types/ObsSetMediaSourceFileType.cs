using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Media Source File",
        description: "Change the file or URL played by a media source in OBS",
        icon: MdiIcons.Video,
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
            var fields = new List<SubActionUIField>();

            IOBSConnectionManager? connectionManager = null;

            if (serviceProvider != null)
            {
                connectionManager = serviceProvider.GetService<IOBSConnectionManager>();
                if (connectionManager != null)
                {
                    var connections = Task.Run(async () => await connectionManager.GetAllConnectionsAsync()).GetAwaiter().GetResult();
                    fields.Add(new SubActionUIField
                    {
                        PropertyName = nameof(OBSConnectionId),
                        Label = "OBS Connection",
                        FieldType = UIFieldType.Select,
                        Required = true,
                        SelectOptions = connections.Select(c => new SelectOption { Id = c.Id, Name = c.Name }).ToList()
                    });
                }
            }

            string[]? inputs = null;

            if (connectionManager != null)
            {
                try
                {
                    var managedConnections = connectionManager.GetAllManagedConnections();
                    ManagedOBSConnection? conn = null;
                    if (OBSConnectionId.HasValue)
                        conn = managedConnections.FirstOrDefault(x => x.Id == OBSConnectionId.Value && x.IsConnected);

                    if (conn != null)
                    {
                        conn.Execute(obs =>
                        {
                            var allInputs = obs.GetInputList();
                            inputs = [.. allInputs
                                .Where(i => MediaInputKinds.Contains(i.InputKind, StringComparer.OrdinalIgnoreCase)
                                         || MediaInputKinds.Contains(i.UnversionedKind, StringComparer.OrdinalIgnoreCase))
                                .Select(i => i.InputName)
                                .Order()];
                        });
                    }
                }
                catch { }
            }

            fields.AddRange(new[]
            {
                new SubActionUIField
                {
                    PropertyName = nameof(InputName),
                    Label = "Media Source Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null && inputs.Length > 0
                        ? "Select the media source to update"
                        : "Media source name (connect OBS to see available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilePath),
                    Label = "File Path",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Full path to the media file (e.g. C:\\videos\\myvideo.mp4). Supports {variables}."
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            });

            return fields;
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

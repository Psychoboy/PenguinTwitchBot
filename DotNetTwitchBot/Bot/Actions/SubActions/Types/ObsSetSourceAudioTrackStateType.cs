using System.ComponentModel.DataAnnotations.Schema;
using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.ObsConnector;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Audio Track State",
        description: "Enable or disable an audio mix track for a source in OBS",
        icon: MdiIcons.VolumeHigh,
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
                            inputs = [.. allInputs.Select(i => i.InputName).Order()];
                        });
                    }
                }
                catch { }
            }

            var trackOptions = Enumerable.Range(1, 6)
                .Select(t => new SelectOption { Id = t, Name = $"Track {t}" })
                .ToList();

            fields.AddRange(new[]
            {
                new SubActionUIField
                {
                    PropertyName = nameof(InputName),
                    Label = "Source Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null && inputs.Length > 0
                        ? "Select the audio source"
                        : "Source name (connect OBS to see available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(TrackNumber),
                    Label = "Track Number",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = trackOptions,
                    HelperText = "Audio mix track to enable or disable (1–6)"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(TrackEnabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success",
                    HelperText = "Turn on to enable the track, off to disable it"
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
            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Source Name is required";
            if (!values.TryGetValue(nameof(TrackNumber), out var t) || t == null)
                return "Track Number is required";
            return null;
        }
    }
}

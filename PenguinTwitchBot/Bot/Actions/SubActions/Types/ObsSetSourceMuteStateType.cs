using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Mute State",
        description: "Mute or unmute a source in OBS",
        icon: MdiIcons.Microphone,
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
                        SelectOptions = connections.Select(c => new SelectOption
                        {
                            Id = c.Id,
                            Name = c.Name
                        }).ToList()
                    });
                }
            }

            string[]? inputs = null;

            if (connectionManager != null)
            {
                try
                {
                    var managedConnections = connectionManager.GetAllManagedConnections();
                    ManagedOBSConnection? connectedConnection = null;
                    if (OBSConnectionId.HasValue)
                    {
                        connectedConnection = managedConnections.FirstOrDefault(x => x.Id == OBSConnectionId.Value && x.IsConnected);
                    }

                    if (connectedConnection != null)
                    {
                        connectedConnection.Execute(obs =>
                        {
                            var inputList = obs.GetInputList();
                            inputs = [.. inputList.Select(i => i.InputName).Order()];
                        });
                    }
                }
                catch (Exception)
                {
                    // Fall back to text input
                }
            }

            fields.AddRange(new[]
            {
                new SubActionUIField
                {
                    PropertyName = nameof(InputName),
                    Label = "Input Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null
                        ? "Select the audio input to mute/unmute"
                        : "Input name (connect an OBS instance to see available inputs)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Muted),
                    Label = "Muted",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Error",
                    HelperText = "Turn on to mute the input, off to unmute it"
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

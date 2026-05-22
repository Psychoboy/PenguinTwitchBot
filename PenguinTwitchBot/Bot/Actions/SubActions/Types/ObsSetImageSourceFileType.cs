using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Image Source File",
        description: "Change the image file displayed by an image source in OBS",
        icon: MdiIcons.Image,
        color: "Warning",
        tableName: "subactions_obs_setimagesourcefile")]
    public class ObsSetImageSourceFileType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] ImageInputKinds = ["image_source"];

        public ObsSetImageSourceFileType()
        {
            SubActionTypes = SubActionTypes.ObsSetImageSourceFile;
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
                                .Where(i => ImageInputKinds.Contains(i.InputKind, StringComparer.OrdinalIgnoreCase))
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
                    Label = "Image Source Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null && inputs.Length > 0
                        ? "Select the image source to update"
                        : "Image source name (connect OBS to see available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilePath),
                    Label = "File Path",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Full path to the image file (e.g. C:\\images\\myimage.png). Supports {variables}."
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
                return "Image Source Name is required";
            if (!values.TryGetValue(nameof(FilePath), out var f) || string.IsNullOrWhiteSpace(f as string))
                return "File Path is required";
            return null;
        }
    }
}

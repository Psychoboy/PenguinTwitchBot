using System.ComponentModel.DataAnnotations.Schema;
using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.ObsConnector;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Color Source Color",
        description: "Change the color of a color source in OBS",
        icon: MdiIcons.Palette,
        color: "Warning",
        tableName: "subactions_obs_setcolorsourcecolor")]
    public class ObsSetColorSourceColorType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] ColorInputKinds = ["color_source_v3", "color_source_v2", "color_source"];

        public ObsSetColorSourceColorType()
        {
            SubActionTypes = SubActionTypes.ObsSetColorSourceColor;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        /// <summary>
        /// Color in #RRGGBB or #AARRGGBB hex format. Alpha defaults to FF if not specified.
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string Color { get; set; } = "#FFFFFF";

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
                                .Where(i => ColorInputKinds.Contains(i.InputKind, StringComparer.OrdinalIgnoreCase))
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
                    Label = "Color Source Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null && inputs.Length > 0
                        ? "Select the color source to update"
                        : "Color source name (connect OBS to see available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Color),
                    Label = "Color (Hex)",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Hex color in #RRGGBB or #AARRGGBB format (e.g. #FF0000 for red, #80FF0000 for 50% transparent red)"
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
            { nameof(Color), Color },
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
            if (values.TryGetValue(nameof(Color), out var c)) Color = c as string ?? "#FFFFFF";
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";
            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Color Source Name is required";
            if (!values.TryGetValue(nameof(Color), out var c) || string.IsNullOrWhiteSpace(c as string))
                return "Color is required";
            var colorStr = (c as string ?? "").TrimStart('#');
            if (colorStr.Length != 6 && colorStr.Length != 8)
                return "Color must be in #RRGGBB or #AARRGGBB format";
            return null;
        }
    }
}

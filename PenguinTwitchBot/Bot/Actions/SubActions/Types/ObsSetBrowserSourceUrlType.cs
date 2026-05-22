using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Browser Source URL",
        description: "Change the URL of a browser source in OBS",
        icon: MdiIcons.Web,
        color: "Warning",
        tableName: "subactions_obs_setbrowsersourceurl")]
    public class ObsSetBrowserSourceUrlType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] BrowserInputKinds = ["browser_source"];

        public ObsSetBrowserSourceUrlType()
        {
            SubActionTypes = SubActionTypes.ObsSetBrowserSourceUrl;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string Url { get; set; } = string.Empty;

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
                                .Where(i => BrowserInputKinds.Contains(i.InputKind, StringComparer.OrdinalIgnoreCase))
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
                    Label = "Browser Source Name",
                    FieldType = inputs != null && inputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = inputs,
                    HelperText = inputs != null && inputs.Length > 0
                        ? "Select the browser source to update"
                        : "Browser source name (connect OBS to see available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Url),
                    Label = "URL",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The URL to set for the browser source. Supports {variables}."
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
            { nameof(Url), Url },
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
            if (values.TryGetValue(nameof(Url), out var u)) Url = u as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";
            if (!values.TryGetValue(nameof(InputName), out var n) || string.IsNullOrWhiteSpace(n as string))
                return "Browser Source Name is required";
            if (!values.TryGetValue(nameof(Url), out var u) || string.IsNullOrWhiteSpace(u as string))
                return "URL is required";
            return null;
        }
    }
}

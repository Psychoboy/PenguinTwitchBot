using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Text",
        description: "Update the text content of a text source in OBS",
        icon: MdiIcons.Text,
        color: "Warning",
        tableName: "subactions_obs_settext")]
    public class ObsSetTextType : SubActionType, ISubActionUIProvider
    {
        private static readonly string[] TextInputKinds = ["text_gdiplus_v3", "text_gdiplus_v2", "text_gdiplus", "text_ft2_source_v2", "text_ft2_source"];

        public ObsSetTextType()
        {
            SubActionTypes = SubActionTypes.ObsSetText;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string InputName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string TextContent { get; set; } = string.Empty;

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

            string[]? textInputs = null;

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
                            var allInputs = obs.GetInputList();
                            textInputs = [.. allInputs
                                .Where(i => TextInputKinds.Contains(i.InputKind, StringComparer.OrdinalIgnoreCase))
                                .Select(i => i.InputName)
                                .Order()];
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
                    Label = "Text Source Name",
                    FieldType = textInputs != null && textInputs.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = textInputs,
                    HelperText = textInputs != null && textInputs.Length > 0
                        ? "Select the text source to update"
                        : "Text source name (connect an OBS instance to see available text sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(TextContent),
                    Label = "Text",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "The text to display. Supports {variables}."
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
                { nameof(TextContent), TextContent },
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

            if (values.TryGetValue(nameof(TextContent), out var textContent))
                TextContent = textContent as string ?? "";

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(InputName), out var inputName) || string.IsNullOrWhiteSpace(inputName as string))
                return "Text Source Name is required";

            if (!values.TryGetValue(nameof(TextContent), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Text content is required";

            return null;
        }
    }
}

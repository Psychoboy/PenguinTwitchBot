using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Visibility",
        description: "Show or hide a source in a scene",
        icon: MdiIcons.Eye,
        color: "Warning",
        tableName: "subactions_obs_setsourcevisibility")]
    public class ObsSetSourceVisibilityType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSourceVisibilityType()
        {
            SubActionTypes = SubActionTypes.ObsSetSourceVisibility;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string SourceName { get; set; } = string.Empty;

        public bool Visible { get; set; } = true;

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

            string[]? scenes = null;
            string[]? sources = null;

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
                            var sceneList = obs.GetSceneList();
                            scenes = [.. sceneList.Scenes.Select(s => s.Name).Order()];

                            if (!string.IsNullOrEmpty(SceneName))
                            {
                                try
                                {
                                    var items = obs.GetSceneItemList(SceneName);
                                    sources = [.. items.Select(i => i.SourceName).Order()];
                                }
                                catch
                                {
                                    // Fall back to text input
                                }
                            }
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
                    PropertyName = nameof(SceneName),
                    Label = "Scene Name",
                    FieldType = scenes != null && scenes.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = scenes,
                    HelperText = scenes != null
                        ? "Select the scene containing the source"
                        : "Scene name (connect an OBS instance to see available scenes)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(SourceName),
                    Label = "Source Name",
                    FieldType = sources != null && sources.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = sources,
                    HelperText = !string.IsNullOrEmpty(SceneName) && sources != null && sources.Length > 0
                        ? $"Select a source from scene '{SceneName}'"
                        : !string.IsNullOrEmpty(SceneName)
                            ? $"No sources found in scene '{SceneName}' (you can type a source name manually)"
                            : "Select a scene first to see available sources",
                    DependsOn = [nameof(SceneName)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Visible),
                    Label = "Visible",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success",
                    HelperText = "Turn on to show the source, off to hide it"
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
                { nameof(SceneName), SceneName },
                { nameof(SourceName), SourceName },
                { nameof(Visible), Visible },
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

            if (values.TryGetValue(nameof(SceneName), out var scene))
                SceneName = scene as string ?? "";

            if (values.TryGetValue(nameof(SourceName), out var source))
                SourceName = source as string ?? "";

            if (values.TryGetValue(nameof(Visible), out var visible))
                Visible = visible as bool? ?? true;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            if (!values.TryGetValue(nameof(SourceName), out var source) || string.IsNullOrWhiteSpace(source as string))
                return "Source Name is required";

            return null;
        }
    }
}

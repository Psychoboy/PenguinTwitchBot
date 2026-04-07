using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.ObsConnector;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Scene",
        description: "Change the current scene in OBS",
        icon: MdiIcons.Video,
        color: "Warning",
        tableName: "subactions_obs_setscene")]
    public class ObsSetSceneType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSceneType()
        {
            SubActionTypes = SubActionTypes.ObsSetScene;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            var fields = new List<SubActionUIField>();

            ObsConnector.IOBSConnectionManager? connectionManager = null;

            // OBS Connection selector
            if (serviceProvider != null)
            {
                connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
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

            // Scene selector - dynamically load from the first connected OBS instance
            string[]? scenes = null;
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
                        });
                    }
                }
                catch (Exception ex)
                {
                    // If we can't get scenes, fall back to text input
                    // Log error but don't break the UI
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
                        ? "Select the OBS scene to switch to" 
                        : "Scene name (connect an OBS instance to see available scenes)",
                    DependsOn = [nameof(OBSConnectionId)]  // Declare that SceneName depends on OBSConnectionId
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
            {
                // Scene name can come as the scene name string directly when using dropdown
                SceneName = scene as string ?? "";
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            return null;
        }
    }
}

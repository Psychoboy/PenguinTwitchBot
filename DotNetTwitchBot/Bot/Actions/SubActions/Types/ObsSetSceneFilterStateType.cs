using System.ComponentModel.DataAnnotations.Schema;
using DotNetTwitchBot.Bot.Actions.SubActions.UI;
using DotNetTwitchBot.Bot.ObsConnector;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Scene Filter State",
        description: "Enable or disable a filter on an OBS scene",
        icon: MdiIcons.FilterVariant,
        color: "Warning",
        tableName: "subactions_obs_setscenefilterstate")]
    public class ObsSetSceneFilterStateType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSceneFilterStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetSceneFilterState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SceneName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string FilterName { get; set; } = string.Empty;

        public bool FilterEnabled { get; set; } = true;

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

            // Dynamically load scenes and filters from the first connected OBS instance
            string[]? scenes = null;
            string[]? filters = null;

            if (connectionManager != null)
            {
                try
                {
                    var managedConnections = connectionManager.GetAllManagedConnections();
                    ManagedOBSConnection? connectedConnection = null;
                    if (OBSConnectionId.HasValue) {
                       connectedConnection = managedConnections.FirstOrDefault(x => x.Id == OBSConnectionId.Value && x.IsConnected);
                    }

                    if (connectedConnection != null)
                    {
                        connectedConnection.Execute(obs =>
                        {
                            // Get scenes
                            var sceneList = obs.GetSceneList();
                            scenes = [.. sceneList.Scenes.Select(s => s.Name).Order()];

                            // Get filters based on SceneName property
                            var filterSet = new HashSet<string>();

                            // If SceneName is set (either from editing or from user selection), load filters from that specific scene
                            if (!string.IsNullOrEmpty(SceneName))
                            {
                                try
                                {
                                    var sceneFilters = obs.GetSourceFilterList(SceneName);
                                    foreach (var filter in sceneFilters)
                                    {
                                        filterSet.Add(filter.Name);
                                    }
                                }
                                catch
                                {
                                    // If we can't get filters from the selected scene, leave empty
                                    // User can manually type filter name if needed
                                }
                            }

                            filters = filterSet.Count > 0 ? [.. filterSet.Order()] : null;
                        });
                    }
                }
                catch (Exception)
                {
                    // If we can't get scenes/filters, fall back to text input
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
                        ? "Select the scene containing the filter" 
                        : "Scene name (connect an OBS instance to see available scenes)",
                    DependsOn = [nameof(OBSConnectionId)]  // Declare that SceneName depends on OBSConnectionId
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilterName),
                    Label = "Filter Name",
                    FieldType = filters != null && filters.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = filters,
                    HelperText = !string.IsNullOrEmpty(SceneName) && filters != null && filters.Length > 0
                        ? $"Select a filter from scene '{SceneName}'"
                        : !string.IsNullOrEmpty(SceneName) && (filters == null || filters.Length == 0)
                            ? $"No filters found on scene '{SceneName}' (you can type a filter name manually)"
                            : "Select a scene first to see available filters",
                    DependsOn = [nameof(SceneName)]  // Declare that FilterName depends on SceneName
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilterEnabled),
                    Label = "Enable Filter",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success",
                    HelperText = "Turn this on to enable the filter, off to disable it"
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
                { nameof(FilterName), FilterName },
                { nameof(FilterEnabled), FilterEnabled },
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

            if (values.TryGetValue(nameof(FilterName), out var filter))
                FilterName = filter as string ?? "";

            if (values.TryGetValue(nameof(FilterEnabled), out var filterEnabled))
                FilterEnabled = filterEnabled as bool? ?? true;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(SceneName), out var scene) || string.IsNullOrWhiteSpace(scene as string))
                return "Scene Name is required";

            if (!values.TryGetValue(nameof(FilterName), out var filter) || string.IsNullOrWhiteSpace(filter as string))
                return "Filter Name is required";

            return null;
        }
    }
}

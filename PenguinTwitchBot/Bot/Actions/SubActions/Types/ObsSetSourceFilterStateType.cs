using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Bot.ObsConnector;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Filter State",
        description: "Enable or disable a filter on any OBS source (input or scene)",
        icon: MdiIcons.FilterVariant,
        color: "Warning",
        tableName: "subactions_obs_setsourcefilterstate")]
    public class ObsSetSourceFilterStateType : SubActionType, ISubActionUIProvider
    {
        public ObsSetSourceFilterStateType()
        {
            SubActionTypes = SubActionTypes.ObsSetSourceFilterState;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string SourceName { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string FilterName { get; set; } = string.Empty;

        public bool FilterEnabled { get; set; } = true;

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

            string[]? sources = null;
            string[]? filters = null;

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
                            // Combine scenes and inputs into a single source list
                            var allNames = new List<string>();
                            var sceneList = obs.GetSceneList();
                            allNames.AddRange(sceneList.Scenes.Select(s => s.Name));
                            var inputList = obs.GetInputList();
                            allNames.AddRange(inputList.Select(i => i.InputName));
                            sources = [.. allNames.Distinct().Order()];

                            if (!string.IsNullOrEmpty(SourceName))
                            {
                                try
                                {
                                    var sourceFilters = obs.GetSourceFilterList(SourceName);
                                    filters = sourceFilters.Count > 0
                                        ? [.. sourceFilters.Select(f => f.Name).Order()]
                                        : null;
                                }
                                catch { }
                            }
                        });
                    }
                }
                catch { }
            }

            fields.AddRange(new[]
            {
                new SubActionUIField
                {
                    PropertyName = nameof(SourceName),
                    Label = "Source Name",
                    FieldType = sources != null && sources.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = sources,
                    HelperText = sources != null && sources.Length > 0
                        ? "Select the source (scene or input) that has the filter"
                        : "Source name (connect OBS to see all available sources)",
                    DependsOn = [nameof(OBSConnectionId)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilterName),
                    Label = "Filter Name",
                    FieldType = filters != null && filters.Length > 0 ? UIFieldType.Select : UIFieldType.Text,
                    Required = true,
                    Options = filters,
                    HelperText = !string.IsNullOrEmpty(SourceName) && filters != null && filters.Length > 0
                        ? $"Select a filter from '{SourceName}'"
                        : !string.IsNullOrEmpty(SourceName)
                            ? $"No filters found on '{SourceName}' (you can type a filter name manually)"
                            : "Select a source first to see available filters",
                    DependsOn = [nameof(SourceName)]
                },
                new SubActionUIField
                {
                    PropertyName = nameof(FilterEnabled),
                    Label = "Enable Filter",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success",
                    HelperText = "Turn on to enable the filter, off to disable it"
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
            { nameof(SourceName), SourceName },
            { nameof(FilterName), FilterName },
            { nameof(FilterEnabled), FilterEnabled },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(OBSConnectionId), out var connId))
            {
                if (connId is string s && int.TryParse(s, out var i)) OBSConnectionId = i;
                else if (connId is int intId) OBSConnectionId = intId;
            }
            if (values.TryGetValue(nameof(SourceName), out var sn)) SourceName = sn as string ?? "";
            if (values.TryGetValue(nameof(FilterName), out var fn)) FilterName = fn as string ?? "";
            if (values.TryGetValue(nameof(FilterEnabled), out var fe)) FilterEnabled = fe as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var e)) Enabled = e as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";
            if (!values.TryGetValue(nameof(SourceName), out var sn) || string.IsNullOrWhiteSpace(sn as string))
                return "Source Name is required";
            if (!values.TryGetValue(nameof(FilterName), out var fn) || string.IsNullOrWhiteSpace(fn as string))
                return "Filter Name is required";
            return null;
        }
    }
}

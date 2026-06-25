using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Source Filter State",
        description: "Enable or disable a filter on any OBS source (input or scene)",
        icon: "mdi-filter-variant",
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
            return new List<SubActionUIField>
            {
                new SubActionUIField
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            };
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

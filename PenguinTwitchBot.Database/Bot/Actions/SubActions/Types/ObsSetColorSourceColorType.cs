using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Set Color Source Color",
        description: "Change the color of a color source in OBS",
        icon: "mdi-palette",
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

using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;
using OBSWebsocketDotNet.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OBS - Trigger Hotkey",
        description: "Trigger a hotkey in OBS",
        icon: "mdi-keyboard",
        color: "Warning",
        tableName: "subactions_obs_triggerhotkey")]
    public class ObsTriggerHotkeyType : SubActionType, ISubActionUIProvider
    {
        public ObsTriggerHotkeyType()
        {
            SubActionTypes = SubActionTypes.ObsTriggerHotkey;
        }

        public int? OBSConnectionId { get; set; }

        [Column(TypeName = "TEXT")]
        public string HotkeyName { get; set; } = string.Empty;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            var hotkeyOptions = Enum.GetNames<OBSHotkey>()
                .Select(name => new SelectOption
                {
                    Value = name,
                    Name = FormatHotkeyName(name),
                })
                .ToList();

            return
            [
                new SubActionUIField
                {
                    PropertyName = nameof(HotkeyName),
                    Label = "Hotkey",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = hotkeyOptions,
                    HelperText = "Select the OBS hotkey to trigger"
                },
                new SubActionUIField
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(OBSConnectionId), OBSConnectionId },
                { nameof(HotkeyName), HotkeyName },
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

            if (values.TryGetValue(nameof(HotkeyName), out var hotkey))
                HotkeyName = hotkey as string ?? "";

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(OBSConnectionId), out var connId) || connId == null)
                return "OBS Connection is required";

            if (!values.TryGetValue(nameof(HotkeyName), out var hotkey) || string.IsNullOrWhiteSpace(hotkey as string))
                return "Hotkey Name is required";

            return null;
        }

        private static string FormatHotkeyName(string enumName)
        {
            // Remove OBS_KEY_ prefix if present
            if (enumName.StartsWith("OBS_KEY_"))
            {
                return enumName.Substring(8);  // Remove "OBS_KEY_" (8 characters)
            }
            return enumName;
        }
    }
}

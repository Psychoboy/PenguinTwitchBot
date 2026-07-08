using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Reset Cooldowns",
        description: "Reset the current action command cooldowns for the triggering user and/or globally",
        icon: "mdi-timer-refresh",
        color: "Warning",
        tableName: "subactions_resetcooldowns")]
    public class ResetCooldownsType : SubActionType, ISubActionUIProvider
    {
        public ResetCooldownsType()
        {
            SubActionTypes = SubActionTypes.ResetCooldowns;
        }

        public string CommandName { get; set; } = "%" + ActionExecutionVariableKeys.CooldownCommandName + "%";
        public string UserName { get; set; } = "%" + ActionExecutionVariableKeys.CooldownUserName + "%";
        public bool ResetUserCooldown { get; set; } = true;
        public bool ResetGlobalCooldown { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [
                new()
                {
                    PropertyName = nameof(CommandName),
                    Label = "Command",
                    FieldType = UIFieldType.Text,
                    HelperText = "Defaults to the current action trigger command. Use %" + ActionExecutionVariableKeys.CooldownCommandName + "% if you want to keep it dynamic.",
                    Clearable = true
                },
                new()
                {
                    PropertyName = nameof(UserName),
                    Label = "User",
                    FieldType = UIFieldType.Text,
                    HelperText = "Defaults to the triggering user. Use %" + ActionExecutionVariableKeys.CooldownUserName + "% if you want to keep it dynamic.",
                    Clearable = true
                },
                new()
                {
                    PropertyName = nameof(ResetUserCooldown),
                    Label = "Reset User Cooldown",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Warning"
                },
                new()
                {
                    PropertyName = nameof(ResetGlobalCooldown),
                    Label = "Reset Global Cooldown",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Warning"
                },
                new()
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
                { nameof(CommandName), CommandName },
                { nameof(UserName), UserName },
                { nameof(ResetUserCooldown), ResetUserCooldown },
                { nameof(ResetGlobalCooldown), ResetGlobalCooldown },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(CommandName), out var commandName))
            {
                CommandName = commandName?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(UserName), out var userName))
            {
                UserName = userName?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(ResetUserCooldown), out var resetUserCooldown) && bool.TryParse(resetUserCooldown?.ToString(), out var parsedResetUserCooldown))
            {
                ResetUserCooldown = parsedResetUserCooldown;
            }

            if (values.TryGetValue(nameof(ResetGlobalCooldown), out var resetGlobalCooldown) && bool.TryParse(resetGlobalCooldown?.ToString(), out var parsedResetGlobalCooldown))
            {
                ResetGlobalCooldown = parsedResetGlobalCooldown;
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled) && bool.TryParse(enabled?.ToString(), out var parsedEnabled))
            {
                Enabled = parsedEnabled;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(ResetUserCooldown), out var resetUserCooldown) || !bool.TryParse(resetUserCooldown?.ToString(), out var parsedResetUserCooldown))
            {
                parsedResetUserCooldown = false;
            }

            if (!values.TryGetValue(nameof(ResetGlobalCooldown), out var resetGlobalCooldown) || !bool.TryParse(resetGlobalCooldown?.ToString(), out var parsedResetGlobalCooldown))
            {
                parsedResetGlobalCooldown = false;
            }

            if (!parsedResetUserCooldown && !parsedResetGlobalCooldown)
            {
                return "Select at least one cooldown reset option.";
            }

            return null;
        }
    }
}
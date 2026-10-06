using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for Command and Keyword triggers.
/// </summary>
public class CommandTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType is TriggerTypes.Command or TriggerTypes.Keyword;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();

        var label = trigger.Type switch
        {
            TriggerTypes.Command => "Command",
            TriggerTypes.Keyword => "Keyword",
            _ => "Command"
        };
        var source = sourcePrefix != null ? $"{sourcePrefix} ({label}: {triggerName})" : $"Trigger: {label} ({triggerName})";

        variables.SetVariable("User", "The username of the chatter executing the command", source, category, "penguin_fan");
        variables.SetVariable("Name", "The username of the chatter", source, category, "penguin_fan");
        variables.SetVariable("DisplayName", "The display name of the chatter", source, category, "Penguin_Fan");
        variables.SetVariable("Args", "The arguments passed after the command", source, category, "arg1 arg2");
        variables.SetVariable("TargetUser", "The targeted user (either 1st argument or command invoker)", source, category, "target_viewer");
        variables.SetVariable("IsMod", "True if the chatter is a channel moderator", source, category, "true");
        variables.SetVariable("IsVip", "True if the chatter is a channel VIP", source, category, "false");
        variables.SetVariable("IsSub", "True if the chatter is a channel subscriber", source, category, "true");
        variables.SetVariable("IsBroadcaster", "True if the chatter is the broadcaster", source, category, "false");
        variables.SetVariable("Channel", "The name of the Twitch channel", source, category, "channel_name");
    }
}


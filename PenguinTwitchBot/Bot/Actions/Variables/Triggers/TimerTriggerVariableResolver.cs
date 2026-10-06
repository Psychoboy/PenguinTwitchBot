using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for Timer triggers.
/// </summary>
public class TimerTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType == TriggerTypes.Timer;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();
        var source = sourcePrefix != null ? $"{sourcePrefix} (Timer: {triggerName})" : $"Trigger: Timer ({triggerName})";

        string timerName = triggerName;
        string timerId = "1";

        if (!string.IsNullOrWhiteSpace(trigger.Configuration))
        {
            try
            {
                using var doc = JsonDocument.Parse(trigger.Configuration);
                if (doc.RootElement.TryGetProperty("TimerGroupName", out var nameProp))
                    timerName = nameProp.GetString() ?? triggerName;
                if (doc.RootElement.TryGetProperty("TimerGroupId", out var idProp))
                    timerId = idProp.ToString();
            }
            catch
            {
                // Ignore JSON parse error
            }
        }

        variables.SetVariable("timer_name", "Name of the timer group that triggered this action", source, category, timerName);
        variables.SetVariable("TimerName", "Name of the timer group (alias of timer_name)", source, category, timerName);
        variables.SetVariable("timer_id", "Database ID of the timer group", source, category, timerId);
        variables.SetVariable("TimerId", "Database ID of the timer group (alias of timer_id)", source, category, timerId);
        variables.SetVariable("TimerInterval", "Configured timer interval in seconds", source, category, "300");
    }
}


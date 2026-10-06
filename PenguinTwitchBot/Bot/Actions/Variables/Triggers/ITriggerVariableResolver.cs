using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Strategy interface for resolving template variables from specific trigger types.
/// </summary>
public interface ITriggerVariableResolver
{
    /// <summary>
    /// Determines whether this resolver can handle the specified trigger type.
    /// </summary>
    bool CanHandle(TriggerTypes triggerType);

    /// <summary>
    /// Populates template variables provided by the trigger.
    /// </summary>
    void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null);
}


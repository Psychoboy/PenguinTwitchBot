using System.Collections.Generic;
using System.Threading.Tasks;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables;

/// <summary>
/// Resolves the context-aware list of available template variables for an action or subaction step.
/// </summary>
public interface IActionVariableResolver
{
    /// <summary>
    /// Resolves all available variables considering global variables, calling actions, triggers, and preceding subactions.
    /// </summary>
    /// <param name="currentActionId">Optional action ID being configured (used to find caller actions via ExecuteAction).</param>
    /// <param name="triggers">Collection of triggers assigned to the action.</param>
    /// <param name="previousSubActions">Collection of sub-actions executing before the current step.</param>
    /// <param name="isCatchSubAction">True if resolving within the Catch sub-action error block.</param>
    /// <param name="visitedActionIds">Set of visited action IDs to prevent recursion loops across ExecuteAction call chains.</param>
    /// <returns>A deduplicated list of unique variables with active sources.</returns>
    Task<List<ActionVariableInfo>> ResolveVariablesAsync(
        int? currentActionId,
        IEnumerable<TriggerType>? triggers,
        IEnumerable<SubActionType>? previousSubActions,
        bool isCatchSubAction = false,
        HashSet<int>? visitedActionIds = null);
}

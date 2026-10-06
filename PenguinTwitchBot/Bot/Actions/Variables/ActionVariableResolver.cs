using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.Variables.Triggers;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Actions.Variables;

/// <summary>
/// Resolves available template variables for actions and subactions.
/// Deduplicates variables case-insensitively while tracking overwrite precedence.
/// </summary>
public class ActionVariableResolver(
    IServiceScopeFactory scopeFactory,
    ILogger<ActionVariableResolver> logger,
    IServiceBackbone? serviceBackbone = null,
    IEnumerable<ITriggerVariableResolver>? triggerResolvers = null) : IActionVariableResolver
{
    private const int MaxCallerDepth = 5;
    private readonly IReadOnlyList<ITriggerVariableResolver> _triggerResolvers =
        (triggerResolvers?.ToList() ?? GetDefaultTriggerResolvers());

    public async Task<List<ActionVariableInfo>> ResolveVariablesAsync(
        int? currentActionId,
        IEnumerable<TriggerType>? triggers,
        IEnumerable<SubActionType>? previousSubActions,
        bool isCatchSubAction = false,
        HashSet<int>? visitedActionIds = null)
    {
        var variables = new Dictionary<string, ActionVariableInfo>(StringComparer.OrdinalIgnoreCase);
        visitedActionIds ??= new HashSet<int>();

        if (currentActionId.HasValue)
        {
            visitedActionIds.Add(currentActionId.Value);
        }

        // 1. Built-in System Global Variables
        ResolveSystemVariables(variables);

        // 2. Caller Actions (via ExecuteAction)
        if (currentActionId.HasValue)
        {
            List<ActionType>? allActions = null;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                allActions = await unitOfWork.Actions.GetAllWithDetailsAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load actions for caller action variable resolution");
            }

            if (allActions != null && allActions.Count > 0)
            {
                ResolveCallerActions(currentActionId.Value, variables, visitedActionIds, allActions, depth: 1);
            }
        }

        // 4. Triggers for the current action
        if (triggers != null)
        {
            foreach (var trigger in triggers)
            {
                ResolveTriggerVariables(trigger, variables);
            }
        }

        // 5. Preceding Subactions
        if (previousSubActions != null)
        {
            var stepIndex = 1;
            foreach (var subAction in previousSubActions)
            {
                ResolveSubActionVariables(subAction, variables, $"Step {stepIndex}");
                stepIndex++;
            }
        }

        // 6. Catch Block Variables
        if (isCatchSubAction)
        {
            variables.SetVariable(
                "ActionErrorMessage",
                "Error message that caused execution to enter the Catch block",
                "Catch Handler",
                "Catch Block",
                "SubAction failed: Connection timeout");
        }

        // 7. Math Helper
        variables.SetVariable(
            "$math",
            "Evaluate arithmetic expressions: $math(expression), e.g. $math(%points% + 10)",
            "System Helper",
            "Special",
            "$math(2 + 2)");

        return variables.Values
            .OrderBy(v => GetCategoryOrder(v.Category))
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetCategoryOrder(string category) => category switch
    {
        "Previous Steps" => 1,
        "Triggers" => 2,
        "Caller Actions" => 3,
        "Catch Block" => 4,
        "System" => 5,
        "Special" => 6,
        _ => 7
    };

    private void ResolveSystemVariables(Dictionary<string, ActionVariableInfo> variables)
    {
        var streamerName = serviceBackbone?.BroadcasterName ?? "Streamer";
        var botName = serviceBackbone?.BotName ?? "Bot";

        variables.SetVariable("bot", "The bot account user name", "System Global", "System", botName);
        variables.SetVariable("streamer", "The streamer / broadcaster channel name", "System Global", "System", streamerName);
        variables.SetVariable("user", "User or initiator of the event/action (defaults to streamer if no trigger user)", "System Global", "System", streamerName);
        variables.SetVariable("date", "Current local date formatted for the local culture", "System Global", "System", DateTime.Now.ToShortDateString());
        variables.SetVariable("time", "Current local time formatted for the local culture with seconds", "System Global", "System", DateTime.Now.ToLongTimeString());
        variables.SetVariable("ticks", "Current system timestamp ticks", "System Global", "System", DateTime.UtcNow.Ticks.ToString());
        variables.SetVariable("random", "Random integer between 1 and 100", "System Global", "System", "42");
    }

    private void ResolveCallerActions(
        int targetActionId,
        Dictionary<string, ActionVariableInfo> variables,
        HashSet<int> visitedActionIds,
        List<ActionType> allActions,
        int depth)
    {
        if (depth > MaxCallerDepth) return;

        try
        {
            var callers = FindCallerActions(targetActionId, allActions);

            foreach (var (callerAction, execSubAction, subActionIdx) in callers)
            {
                if (!callerAction.Id.HasValue || !visitedActionIds.Add(callerAction.Id.Value))
                {
                    continue; // Skip already visited caller action to prevent loops
                }

                var callerLabel = $"Caller: {callerAction.Name}";

                // 1. Caller trigger variables
                if (callerAction.Triggers != null)
                {
                    foreach (var trigger in callerAction.Triggers)
                    {
                        ResolveTriggerVariables(trigger, variables, callerLabel);
                    }
                }

                // 2. Caller subactions preceding the ExecuteAction call
                if (callerAction.SubActions != null)
                {
                    for (int i = 0; i < subActionIdx && i < callerAction.SubActions.Count; i++)
                    {
                        ResolveSubActionVariables(callerAction.SubActions[i], variables, $"{callerLabel} Step {i + 1}", "Caller Actions");
                    }
                }

                // 3. Arguments passed via ExecuteAction
                variables.SetVariable("Args", "Arguments passed from caller action", callerLabel, "Caller Actions", "caller arguments");
                variables.SetVariable("TargetUser", "Target user specified by caller action", callerLabel, "Caller Actions", "TargetViewer");

                // Recursively check callers of the caller
                ResolveCallerActions(callerAction.Id.Value, variables, visitedActionIds, allActions, depth + 1);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve caller actions for Action ID {ActionId}", targetActionId);
        }
    }

    private static List<(ActionType CallerAction, ExecuteActionType ExecSubAction, int SubActionIndex)> FindCallerActions(
        int targetActionId,
        IEnumerable<ActionType> allActions)
    {
        var result = new List<(ActionType CallerAction, ExecuteActionType ExecSubAction, int SubActionIndex)>();

        foreach (var action in allActions)
        {
            if (action.SubActions != null)
            {
                for (int i = 0; i < action.SubActions.Count; i++)
                {
                    if (ContainsExecuteAction(action.SubActions[i], targetActionId, out var matchingExec) && matchingExec != null)
                    {
                        result.Add((action, matchingExec, i));
                    }
                }
            }

            if (action.CatchSubActions != null)
            {
                for (int i = 0; i < action.CatchSubActions.Count; i++)
                {
                    if (ContainsExecuteAction(action.CatchSubActions[i], targetActionId, out var matchingExec) && matchingExec != null)
                    {
                        result.Add((action, matchingExec, action.SubActions?.Count ?? 0));
                    }
                }
            }
        }

        return result;
    }

    private static bool ContainsExecuteAction(
        SubActionType subAction,
        int targetActionId,
        out ExecuteActionType? matchingExec)
    {
        if (subAction is ExecuteActionType exec && exec.ActionId == targetActionId)
        {
            matchingExec = exec;
            return true;
        }

        if (subAction is LogicIfElseType ifElse)
        {
            if (ifElse.TrueSubActions != null)
            {
                foreach (var s in ifElse.TrueSubActions)
                {
                    if (ContainsExecuteAction(s, targetActionId, out matchingExec))
                        return true;
                }
            }

            if (ifElse.FalseSubActions != null)
            {
                foreach (var s in ifElse.FalseSubActions)
                {
                    if (ContainsExecuteAction(s, targetActionId, out matchingExec))
                        return true;
                }
            }
        }

        matchingExec = null;
        return false;
    }

    private void ResolveTriggerVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        var resolver = _triggerResolvers.FirstOrDefault(r => r.CanHandle(trigger.Type));
        if (resolver != null)
        {
            resolver.ResolveVariables(trigger, variables, sourcePrefix);
        }
        else
        {
            var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();
            var source = sourcePrefix != null ? $"{sourcePrefix} ({triggerName})" : $"Trigger: {triggerName}";
            variables.SetVariable("User", "The user or initiator triggering this action", source, "Triggers", "viewer");
        }
    }

    private static void ResolveSubActionVariables(
        SubActionType subAction,
        Dictionary<string, ActionVariableInfo> variables,
        string source,
        string category = "Previous Steps")
    {
        if (!subAction.Enabled) return;

        switch (subAction)
        {
            case MultiCounterType counter:
                if (!string.IsNullOrWhiteSpace(counter.Name))
                {
                    variables.SetVariable($"counter_{counter.Name}", $"Current value of the '{counter.Name}' counter", source, category, "10");
                }
                if (!string.IsNullOrWhiteSpace(counter.DestinationVariable))
                {
                    variables.SetVariable(counter.DestinationVariable, $"Destination variable containing '{counter.Name}' counter value", source, category, "10");
                }
                break;

            case SetVariableType setVar:
                if (!string.IsNullOrWhiteSpace(setVar.Text))
                {
                    variables.SetVariable(setVar.Text, "Variable assigned by Set Variable subaction", source, category, setVar.Value);
                }
                break;

            case GetGlobalVariableType getGlobalVar:
                var targetName = !string.IsNullOrWhiteSpace(getGlobalVar.TargetVariableName)
                    ? getGlobalVar.TargetVariableName
                    : getGlobalVar.Text;
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    var globalName = getGlobalVar.Text;
                    var desc = !string.IsNullOrWhiteSpace(globalName)
                        ? $"Value retrieved from global variable '{globalName}'"
                        : "Value retrieved from global variable";
                    variables.SetVariable(targetName, desc, source, category, "SavedValue");
                }
                break;

            case RandomIntType:
                variables.SetVariable("random_int", "Generated random integer", source, category, "42");
                break;

            case CurrentTimeType:
                variables.SetVariable("currentTime", "Current formatted date and time", source, category, DateTime.Now.ToString("g"));
                break;

            case UptimeType:
                variables.SetVariable("Uptime", "Current stream uptime (e.g. 02:15:30 or Offline)", source, category, "02:15:30");
                break;

            case FollowAgeType:
                variables.SetVariable("Followage", "Viewer follow duration string", source, category, "2 years, 3 months");
                break;

            case WatchTimeType:
                variables.SetVariable("watch_time", "Viewer stream watch time string", source, category, "45 hours");
                break;

            case CheckPointsType:
                variables.SetVariable("TargetPoints", "Loyalty point balance of user", source, category, "1500");
                variables.SetVariable("TargetPointsFormatted", "Formatted point balance with commas", source, category, "1,500");
                break;

            case GiveawayPrizeType:
                variables.SetVariable("Prize", "Name of the active giveaway prize", source, category, "Steam Gift Card");
                break;

            case ExternalApiType:
                variables.SetVariable("ExternalApiResponse", "Raw string response payload from external API", source, category, "{\"status\":\"ok\"}");
                break;

            case FishingType:
                variables.SetVariable("fish_name", "Name of caught fish", source, category, "Largemouth Bass");
                variables.SetVariable("fish_weight", "Weight of caught fish", source, category, "5.4");
                variables.SetVariable("fish_length", "Length of caught fish", source, category, "20.1");
                variables.SetVariable("fish_value", "Gold value of caught fish", source, category, "220");
                variables.SetVariable("fish_rarity", "Rarity tier of caught fish", source, category, "Rare");
                break;

            case FishingTournamentEligibleCatchType:
                variables.SetVariable("fishing_tournament_eligible", "True if catch is eligible for an active tournament", source, category, "true");
                variables.SetVariable("fishing_tournament_id", "Matching tournament ID", source, category, "1");
                variables.SetVariable("fishing_tournament_name", "Matching tournament name", source, category, "Derby");
                variables.SetVariable("fishing_tournament_qualifying", "True if catch placed in qualifying rank", source, category, "true");
                variables.SetVariable("fishing_tournament_match_count", "Number of matching tournaments", source, category, "1");
                break;

            case RaffleGetEntryCountType:
                variables.SetVariable("raffle_entry_count", "Total tickets entered by the user in active raffle", source, category, "5");
                break;

            case RaffleEndType:
                variables.SetVariable("raffle_winner", "Username of the selected raffle winner", source, category, "lucky_viewer");
                variables.SetVariable("raffle_winner_count", "Number of winners drawn", source, category, "1");
                variables.SetVariable("raffle_entries_count", "Total eligible tickets in concluded raffle", source, category, "42");
                break;

            case SelectRandomViewersType:
                variables.SetVariable("SelectedViewerCount", "Total number of viewers chosen", source, category, "3");
                variables.SetVariable("RandomViewer", "Primary selected random viewer username", source, category, "viewer_one");
                variables.SetVariable("RandomViewer1", "First selected random viewer username", source, category, "viewer_one");
                variables.SetVariable("RandomViewer2", "Second selected random viewer username", source, category, "viewer_two");
                break;

            case ForEachViewerType:
                variables.SetVariable("CurrentViewer", "Username of current viewer in loop iteration", source, category, "current_viewer");
                variables.SetVariable("ViewerIndex", "0-based iteration index in loop", source, category, "0");
                break;

            case LogicIfElseType ifElse:
                // Explore subactions inside both True and False branches
                if (ifElse.TrueSubActions != null)
                {
                    int trueIdx = 1;
                    foreach (var trueSub in ifElse.TrueSubActions)
                    {
                        ResolveSubActionVariables(trueSub, variables, $"{source} (True Branch Step {trueIdx})", category);
                        trueIdx++;
                    }
                }
                if (ifElse.FalseSubActions != null)
                {
                    int falseIdx = 1;
                    foreach (var falseSub in ifElse.FalseSubActions)
                    {
                        ResolveSubActionVariables(falseSub, variables, $"{source} (False Branch Step {falseIdx})", category);
                        falseIdx++;
                    }
                }
                break;
        }
    }

    private static IReadOnlyList<ITriggerVariableResolver> GetDefaultTriggerResolvers() =>
    [
        new CommandTriggerVariableResolver(),
        new DefaultCommandTriggerVariableResolver(),
        new TwitchEventTriggerVariableResolver(),
        new FishingTriggerVariableResolver(),
        new TimerTriggerVariableResolver(),
        new MiscellaneousTriggerVariableResolver()
    ];
}

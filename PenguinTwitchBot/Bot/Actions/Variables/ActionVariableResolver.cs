using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Actions.Variables;

/// <summary>
/// Resolves available template variables for actions and subactions.
/// Deduplicates variables case-insensitively while tracking overwrite precedence.
/// </summary>
public class ActionVariableResolver(
    IServiceScopeFactory scopeFactory,
    ILogger<ActionVariableResolver> logger) : IActionVariableResolver
{
    private const int MaxCallerDepth = 5;

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

        // 1. System and Global Variables
        await ResolveGlobalsAsync(variables);

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

        // 3. Triggers for the current action
        if (triggers != null)
        {
            foreach (var trigger in triggers)
            {
                ResolveTriggerVariables(trigger, variables);
            }
        }

        // 4. Preceding Subactions
        if (previousSubActions != null)
        {
            var stepIndex = 1;
            foreach (var subAction in previousSubActions)
            {
                ResolveSubActionVariables(subAction, variables, $"Step {stepIndex}");
                stepIndex++;
            }
        }

        // 5. Catch Block Variables
        if (isCatchSubAction)
        {
            SetVariable(
                variables,
                "ActionErrorMessage",
                "Error message that caused execution to enter the Catch block",
                "Catch Handler",
                "Catch Block",
                "SubAction failed: Connection timeout");
        }

        // 6. Math Helper
        SetVariable(
            variables,
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
        "Globals" => 5,
        "Special" => 6,
        _ => 7
    };

    private async Task ResolveGlobalsAsync(Dictionary<string, ActionVariableInfo> variables)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dbGlobals = await unitOfWork.GlobalVariables.GetAllAsync();
            if (dbGlobals != null)
            {
                foreach (var g in dbGlobals)
                {
                    if (!string.IsNullOrWhiteSpace(g.Name))
                    {
                        SetVariable(
                            variables,
                            g.Name,
                            "Persisted global variable name (load into a local variable using Get Global Variable before template use)",
                            "Database Global",
                            "Globals",
                            exampleValue: null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load database global variables for action variable resolver");
        }
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
                SetVariable(variables, "Args", "Arguments passed from caller action", callerLabel, "Caller Actions", "caller arguments");
                SetVariable(variables, "TargetUser", "Target user specified by caller action", callerLabel, "Caller Actions", "TargetViewer");

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

    private static void ResolveTriggerVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        var category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();

        switch (trigger.Type)
        {
            case TriggerTypes.Command:
            case TriggerTypes.Keyword:
            {
                var label = trigger.Type switch
                {
                    TriggerTypes.Command => "Command",
                    TriggerTypes.Keyword => "Keyword",
                    _ => "Command"
                };
                var source = sourcePrefix != null ? $"{sourcePrefix} ({label}: {triggerName})" : $"Trigger: {label} ({triggerName})";

                SetVariable(variables, "User", "The username of the chatter executing the command", source, category, "penguin_fan");
                SetVariable(variables, "Name", "The username of the chatter", source, category, "penguin_fan");
                SetVariable(variables, "DisplayName", "The display name of the chatter", source, category, "Penguin_Fan");
                SetVariable(variables, "Args", "The arguments passed after the command", source, category, "arg1 arg2");
                SetVariable(variables, "TargetUser", "The targeted user (either 1st argument or command invoker)", source, category, "target_viewer");
                SetVariable(variables, "IsMod", "True if the chatter is a channel moderator", source, category, "true");
                SetVariable(variables, "IsVip", "True if the chatter is a channel VIP", source, category, "false");
                SetVariable(variables, "IsSub", "True if the chatter is a channel subscriber", source, category, "true");
                SetVariable(variables, "IsBroadcaster", "True if the chatter is the broadcaster", source, category, "false");
                SetVariable(variables, "Channel", "The name of the Twitch channel", source, category, "channel_name");
                break;
            }

            case TriggerTypes.DefaultCommand:
            {
                ResolveDefaultCommandVariables(trigger, variables, sourcePrefix, category);
                break;
            }

            case TriggerTypes.TwitchEvent:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Event: {triggerName})" : $"Trigger: Twitch Event ({triggerName})";
                ResolveTwitchEventVariables(triggerName, variables, source, category);
                break;
            }

            case TriggerTypes.FishCatch:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Catch: {triggerName})" : $"Trigger: Fish Catch ({triggerName})";
                SetVariable(variables, "User", "Chatter who caught the fish", source, category, "angler123");
                SetVariable(variables, "fish_name", "Name of the caught fish species", source, category, "Rainbow Trout");
                SetVariable(variables, "fish_type", "Name of the caught fish species (alias of fish_name)", source, category, "Rainbow Trout");
                SetVariable(variables, "fish_type_id", "Database ID of the fish species", source, category, "1");
                SetVariable(variables, "fish_weight", "Weight of the caught fish in lbs/kg", source, category, "4.2");
                SetVariable(variables, "fish_length", "Length of the caught fish in inches/cm", source, category, "18.5");
                SetVariable(variables, "fish_value", "Gold value awarded for the catch", source, category, "150");
                SetVariable(variables, "fish_gold", "Gold value awarded for the catch (alias of fish_value)", source, category, "150");
                SetVariable(variables, "fish_rarity", "Rarity tier of the catch", source, category, "Rare");
                SetVariable(variables, "fish_stars", "Star quality rating of the catch", source, category, "2");
                SetVariable(variables, "fish_categories", "Categories assigned to the caught fish", source, category, "River, Freshwater");
                break;
            }

            case TriggerTypes.FishingItemBroken:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Broken Item: {triggerName})" : $"Trigger: Fishing Item Broken ({triggerName})";
                SetVariable(variables, "User", "Chatter whose gear broke while fishing", source, category, "angler123");
                SetVariable(variables, "broken_item_name", "Name of the broken fishing item", source, category, "Reinforced Rod");
                SetVariable(variables, "broken_item_slot", "Equipment slot of the broken item", source, category, "Rod");
                SetVariable(variables, "broken_item_shop_id", "Shop item ID of the broken item", source, category, "1");
                SetVariable(variables, "broken_item_cost", "Gold purchase cost of the broken item", source, category, "250");
                SetVariable(variables, "broken_item_replaced", "True if the item was automatically replaced", source, category, "true");
                SetVariable(variables, "broken_items_count", "Total count of items broken during the attempt", source, category, "1");
                break;
            }

            case TriggerTypes.FishingAccident:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Accident: {triggerName})" : $"Trigger: Fishing Accident ({triggerName})";
                SetVariable(variables, "User", "Chatter who experienced the fishing accident", source, category, "angler123");
                SetVariable(variables, "accident_type", "Type of fishing accident outcome", source, category, "SnappedLine");
                SetVariable(variables, "accident_name", "Flavor name and description of the accident", source, category, "Line Snapped");
                SetVariable(variables, "accident_reason", "Description of what caused the accident", source, category, "Tension too high");
                SetVariable(variables, "accident_items_lost", "Comma-separated list of items lost in the accident", source, category, "Lure, Hook");
                SetVariable(variables, "accident_gear", "Gear affected in the accident", source, category, "Rod");
                SetVariable(variables, "accident_gold_lost", "Total gold lost as a result of the accident", source, category, "50");
                break;
            }

            case TriggerTypes.FishingTournamentCatch:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Tournament Catch: {triggerName})" : $"Trigger: Fishing Tournament Catch ({triggerName})";
                SetVariable(variables, "User", "Chatter participating in the tournament", source, category, "angler123");
                SetVariable(variables, "fish_name", "Fish species caught", source, category, "Golden Bass");
                SetVariable(variables, "fish_type", "Fish species caught (alias of fish_name)", source, category, "Golden Bass");
                SetVariable(variables, "fish_weight", "Fish weight in lbs/kg", source, category, "6.8");
                SetVariable(variables, "fish_length", "Fish length in inches/cm", source, category, "22.4");
                SetVariable(variables, "fish_rarity", "Rarity tier of the catch", source, category, "Rare");
                SetVariable(variables, "fish_stars", "Star quality rating of the catch", source, category, "3");
                SetVariable(variables, "fish_gold", "Gold awarded for the catch", source, category, "250");
                SetVariable(variables, "fishing_tournament_name", "Name of the active tournament", source, category, "Weekend Derby");
                SetVariable(variables, "fishing_tournament_eligible", "True if catch satisfies tournament conditions", source, category, "true");
                SetVariable(variables, "fishing_tournament_id", "ID of tournament", source, category, "1");
                SetVariable(variables, "fishing_tournament_qualifying_names", "Names of qualifying tournaments matched by the catch", source, category, "Weekend Derby");
                SetVariable(variables, "fishing_tournament_reward_qualifying", "True if user qualifies for a reward placement", source, category, "true");
                break;
            }

            case TriggerTypes.FishingTournamentStart:
            case TriggerTypes.FishingTournamentEnd:
            {
                var isEnd = trigger.Type == TriggerTypes.FishingTournamentEnd;
                var label = isEnd ? "Tournament End" : "Tournament Start";
                var source = sourcePrefix != null ? $"{sourcePrefix} ({label}: {triggerName})" : $"Trigger: {label} ({triggerName})";
                SetVariable(variables, "fishing_tournament_id", "Database ID of the fishing tournament", source, category, "1");
                SetVariable(variables, "fishing_tournament_name", "Name of the tournament", source, category, "Weekend Derby");
                SetVariable(variables, "fishing_tournament_description", "Description of the tournament", source, category, "Catch the heaviest bass!");
                SetVariable(variables, "fishing_tournament_status", "Current status of the tournament", source, category, isEnd ? "Completed" : "Active");
                SetVariable(variables, "fishing_tournament_primary_score_category", "Primary scoring criteria", source, category, "Weight");
                SetVariable(variables, "fishing_tournament_starts_at_utc", "Tournament start timestamp (UTC)", source, category, DateTime.UtcNow.ToString("O"));
                SetVariable(variables, "fishing_tournament_ends_at_utc", "Tournament end timestamp (UTC)", source, category, DateTime.UtcNow.AddHours(2).ToString("O"));
                SetVariable(variables, "fishing_tournament_eligible_fish_count", "Total count of eligible fish species", source, category, "3");
                SetVariable(variables, "fishing_tournament_eligible_fish_names", "Comma-separated list of eligible fish species", source, category, "Bass, Trout, Salmon");

                if (isEnd)
                {
                    SetVariable(variables, "fishing_tournament_reward_winner_count", "Total number of rewarded winners", source, category, "3");
                    SetVariable(variables, "fishing_tournament_reward_winner_names", "Comma-separated usernames of tournament winners", source, category, "angler1, angler2, angler3");
                }
                break;
            }

            case TriggerTypes.Timer:
            {
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

                SetVariable(variables, "timer_name", "Name of the timer group that triggered this action", source, category, timerName);
                SetVariable(variables, "TimerName", "Name of the timer group (alias of timer_name)", source, category, timerName);
                SetVariable(variables, "timer_id", "Database ID of the timer group", source, category, timerId);
                SetVariable(variables, "TimerId", "Database ID of the timer group (alias of timer_id)", source, category, timerId);
                SetVariable(variables, "TimerInterval", "Configured timer interval in seconds", source, category, "300");
                break;
            }

            case TriggerTypes.BannedSongRequest:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Banned Song: {triggerName})" : $"Trigger: Banned Song Request ({triggerName})";
                SetVariable(variables, "User", "Chatter who requested the banned song", source, category, "viewer");
                SetVariable(variables, "DisplayName", "Display name of the chatter", source, category, "Viewer");
                SetVariable(variables, "banned_song_id", "YouTube ID of the banned song", source, category, "dQw4w9WgXcQ");
                SetVariable(variables, "banned_song_title", "Title of the banned song", source, category, "Never Bank");
                SetVariable(variables, "banned_song_reason", "Reason why the song was banned", source, category, "Overplayed");
                SetVariable(variables, "banned_song_banned_by", "Streamer or moderator who banned the song", source, category, "streamer");
                SetVariable(variables, "banned_song_url", "YouTube URL of the banned song", source, category, "https://youtu.be/dQw4w9WgXcQ");
                SetVariable(variables, "banned_song_request", "Original song request input string from chat", source, category, "!sr dQw4w9WgXcQ");
                break;
            }

            case TriggerTypes.Manual:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Manual: {triggerName})" : $"Trigger: Manual ({triggerName})";
                SetVariable(variables, "User", "The user or broadcaster manually executing the action", source, category, "broadcaster");
                SetVariable(variables, "UserName", "The login username of the executor", source, category, "broadcaster");
                SetVariable(variables, "DisplayName", "The display name of the executor", source, category, "Broadcaster");
                break;
            }

            default:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} ({triggerName})" : $"Trigger: {triggerName}";
                SetVariable(variables, "User", "The user or initiator triggering this action", source, category, "viewer");
                break;
            }
        }
    }

    private static void ResolveTwitchEventVariables(
        string eventType,
        Dictionary<string, ActionVariableInfo> variables,
        string source,
        string category)
    {
        SetVariable(variables, "User", "The user initiating or targeted in the event", source, category, "chatter_name");
        SetVariable(variables, "UserName", "The login username of the chatter", source, category, "chatter_name");
        SetVariable(variables, "DisplayName", "The display name of the chatter", source, category, "Chatter_Name");

        if (eventType.Contains("Cheer", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Bits", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "Bits", "Total bits cheered or spent", source, category, "500");
            SetVariable(variables, "Message", "Cheer message content", source, category, "Cheer500 Great stream!");
        }
        else if (eventType.Contains("Raid", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "Viewers", "Count of raiding viewers joining the stream", source, category, "42");
        }
        else if (eventType.Contains("Subscribe", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Subscription", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "Tier", "Subscription tier (Tier 1, Tier 2, Tier 3, Prime)", source, category, "Tier 1");
            SetVariable(variables, "Months", "Cumulative total months subscribed", source, category, "6");
            SetVariable(variables, "Streak", "Consecutive subscription streak in months", source, category, "3");
            SetVariable(variables, "Message", "Resubscription message content", source, category, "Love the content!");
            SetVariable(variables, "Total", "Total gift subscriptions in this bundle", source, category, "5");
            SetVariable(variables, "IsGift", "True if this subscription was gifted", source, category, "true");
        }
        else if (eventType.Contains("Point", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Reward", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "RewardTitle", "Title of the redeemed Channel Points custom reward", source, category, "Hydrate");
            SetVariable(variables, "RewardCost", "Point cost of the custom reward", source, category, "250");
            SetVariable(variables, "UserInput", "Viewer text entered when redeeming the reward", source, category, "Take a sip!");
            SetVariable(variables, "RedemptionId", "Twitch redemption UUID", source, category, "uuid-1234");
        }
        else if (eventType.Contains("AdBreak", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "DurationSeconds", "Length of the ad break in seconds", source, category, "90");
            SetVariable(variables, "IsAutomatic", "True if ad break was scheduled automatically", source, category, "false");
        }
        else if (eventType.Contains("Ban", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "Reason", "Reason specified for the ban or timeout", source, category, "Spamming");
            SetVariable(variables, "DurationSeconds", "Timeout duration in seconds (blank or 0 for permanent ban)", source, category, "600");
        }
        else if (eventType.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "StreamStartedAt", "Timestamp when broadcast went live", source, category, DateTime.UtcNow.ToString("O"));
        }
        else if (eventType.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            SetVariable(variables, "StreamDuration", "Total length of the broadcast session", source, category, "03:45:12");
        }
    }

    private static void ResolveDefaultCommandVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix,
        string category)
    {
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();
        var source = sourcePrefix != null ? $"{sourcePrefix} (Default Command: {triggerName})" : $"Trigger: Default Command ({triggerName})";

        // Standard command variables always forwarded on default command executions
        SetVariable(variables, "User", "The username of the chatter executing the command", source, category, "penguin_fan");
        SetVariable(variables, "Name", "The username of the chatter", source, category, "penguin_fan");
        SetVariable(variables, "DisplayName", "The display name of the chatter", source, category, "Penguin_Fan");
        SetVariable(variables, "Command", "The name of the default command", source, category, "spinwheel");
        SetVariable(variables, "Arg", "Arguments passed after the command or event payload", source, category, "arg1");
        SetVariable(variables, "Args", "The arguments passed after the command", source, category, "arg1 arg2");
        SetVariable(variables, "TargetUser", "The targeted user (either 1st argument or command invoker)", source, category, "target_viewer");
        SetVariable(variables, "IsMod", "True if the chatter is a channel moderator", source, category, "true");
        SetVariable(variables, "IsVip", "True if the chatter is a channel VIP", source, category, "false");
        SetVariable(variables, "IsSub", "True if the chatter is a channel subscriber", source, category, "true");
        SetVariable(variables, "IsBroadcaster", "True if the chatter is the broadcaster", source, category, "false");
        SetVariable(variables, "Channel", "The name of the Twitch channel", source, category, "channel_name");

        // Try to parse configuration
        string? commandName = null;
        string? eventType = null;

        if (!string.IsNullOrWhiteSpace(trigger.Configuration))
        {
            try
            {
                var config = JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(trigger.Configuration);
                if (config != null)
                {
                    commandName = config.DefaultCommandName?.Trim();
                    eventType = config.EventType?.Trim();
                }
            }
            catch
            {
                // Fall back to trigger name inference
            }
        }

        var nameLower = triggerName.ToLowerInvariant();

        // Infer commandName if not found in configuration
        if (string.IsNullOrEmpty(commandName))
        {
            if (nameLower.Contains("spinwheel") || nameLower.Contains("wheel")) commandName = "spinwheel";
            else if (nameLower.Contains("gamble")) commandName = "gamble";
            else if (nameLower.Contains("defuse")) commandName = "defuse";
            else if (nameLower.Contains("roll") || nameLower.Contains("dice")) commandName = "roll";
            else if (nameLower.Contains("slot")) commandName = "slot";
            else if (nameLower.Contains("steal")) commandName = "steal";
            else if (nameLower.Contains("heist")) commandName = "heist";
            else if (nameLower.Contains("death")) commandName = "death";
        }

        // Infer eventType if not found in configuration
        if (string.IsNullOrEmpty(eventType) && !string.IsNullOrEmpty(commandName))
        {
            if (commandName == "spinwheel" || nameLower.Contains("result"))
                eventType = DefaultCommandEventTypes.WheelSpinResult;
            else if (nameLower.Contains("jackpot"))
                eventType = DefaultCommandEventTypes.GambleJackpotWin;
            else if (nameLower.Contains("win") && commandName == "gamble")
                eventType = DefaultCommandEventTypes.GambleWin;
            else if (nameLower.Contains("lose") && commandName == "gamble")
                eventType = DefaultCommandEventTypes.GambleLose;
            else if (nameLower.Contains("success") && commandName == "defuse")
                eventType = DefaultCommandEventTypes.DefuseSuccess;
            else if (nameLower.Contains("fail") && commandName == "defuse")
                eventType = DefaultCommandEventTypes.DefuseFailure;
            else if (nameLower.Contains("doubles"))
                eventType = DefaultCommandEventTypes.RollDoubles;
            else if (nameLower.Contains("snakeeyes"))
                eventType = DefaultCommandEventTypes.RollSnakeEyes;
            else if (nameLower.Contains("boxcars"))
                eventType = DefaultCommandEventTypes.RollBoxcars;
            else if (nameLower.Contains("lose") && commandName == "roll")
                eventType = DefaultCommandEventTypes.RollLose;
            else if (nameLower.Contains("threeofakind") || nameLower.Contains("three_of_a_kind"))
                eventType = DefaultCommandEventTypes.SlotsThreeOfAKind;
            else if (nameLower.Contains("twoofakind") || nameLower.Contains("two_of_a_kind"))
                eventType = DefaultCommandEventTypes.SlotsTwoOfAKind;
            else if (nameLower.Contains("lose") && commandName == "slot")
                eventType = DefaultCommandEventTypes.SlotsLose;
            else if (nameLower.Contains("topoor") || nameLower.Contains("to_poor"))
                eventType = DefaultCommandEventTypes.StealToPoor;
            else if (nameLower.Contains("success") && commandName == "steal")
                eventType = DefaultCommandEventTypes.StealSuccess;
            else if (nameLower.Contains("fail") && commandName == "steal")
                eventType = DefaultCommandEventTypes.StealFailed;
            else if (nameLower.Contains("survived"))
                eventType = DefaultCommandEventTypes.HeistUserSurvived;
            else if (nameLower.Contains("caught"))
                eventType = DefaultCommandEventTypes.HeistUserCaught;
            else if (nameLower.Contains("ended"))
                eventType = DefaultCommandEventTypes.HeistEnded;
            else if (nameLower.Contains("started"))
                eventType = DefaultCommandEventTypes.HeistStarted;
            else if (nameLower.Contains("incremented"))
                eventType = DefaultCommandEventTypes.DeathIncremented;
            else if (nameLower.Contains("decremented"))
                eventType = DefaultCommandEventTypes.DeathDecremented;
            else if (nameLower.Contains("reset"))
                eventType = DefaultCommandEventTypes.DeathReset;
            else if (nameLower.Contains("set"))
                eventType = DefaultCommandEventTypes.DeathSet;
        }

        // Add command- and event-specific variables
        switch (commandName?.ToLowerInvariant())
        {
            case "spinwheel":
            case "wheel":
            {
                SetVariable(variables, "WheelSpinResult", "Winning segment label text of the wheel spin", source, category, "500 Points");
                SetVariable(variables, "WinningLabel", "Winning segment label text (alias of WheelSpinResult)", source, category, "500 Points");
                SetVariable(variables, "WinningMessage", "Formatted winning announcement message", source, category, "Congratulations! You won 500 Points!");
                SetVariable(variables, "WheelName", "Name of the wheel that was spun", source, category, "Prizes Wheel");
                SetVariable(variables, "WinningIndex", "Zero-based index of the winning slice", source, category, "2");
                SetVariable(variables, "IsNameWheel", "True if the wheel was spun as a viewer name wheel", source, category, "false");
                break;
            }

            case "gamble":
            {
                if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleJackpotWin, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "JackpotAmount", "Jackpot bonus points won", source, category, "10,000");
                    SetVariable(variables, "WinAmount", "Base points won from the roll", source, category, "500");
                    SetVariable(variables, "TotalWinnings", "Total points won (WinAmount + JackpotAmount)", source, category, "10,500");
                    SetVariable(variables, "RolledValue", "Number rolled on the gamble (1-100)", source, category, "100");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleWin, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "WinAmount", "Points won from the gamble", source, category, "500");
                    SetVariable(variables, "RolledValue", "Number rolled on the gamble (1-100)", source, category, "95");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleLose, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "LoseAmount", "Points lost from the gamble", source, category, "250");
                    SetVariable(variables, "RolledValue", "Number rolled on the gamble (1-100)", source, category, "15");
                }
                else
                {
                    SetVariable(variables, "WinAmount", "Points won from the gamble", source, category, "500");
                    SetVariable(variables, "LoseAmount", "Points lost from the gamble", source, category, "250");
                    SetVariable(variables, "JackpotAmount", "Jackpot bonus points won", source, category, "10,000");
                    SetVariable(variables, "TotalWinnings", "Total points won", source, category, "10,500");
                    SetVariable(variables, "RolledValue", "Number rolled on the gamble (1-100)", source, category, "95");
                }
                break;
            }

            case "defuse":
            {
                SetVariable(variables, "ChosenWire", "The wire chosen by the chatter", source, category, "red");
                SetVariable(variables, "CorrectWire", "The correct wire to defuse the bomb", source, category, "red");

                if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DefuseSuccess, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "WinAmount", "Reward points awarded for defusing the bomb", source, category, "150");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DefuseFailure, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "LoseAmount", "Points lost when the bomb exploded", source, category, "50");
                }
                else
                {
                    SetVariable(variables, "WinAmount", "Reward points awarded for defusing the bomb", source, category, "150");
                    SetVariable(variables, "LoseAmount", "Points lost when the bomb exploded", source, category, "50");
                }
                break;
            }

            case "roll":
            case "dice":
            {
                SetVariable(variables, "Dice1", "Value of the first die rolled (1-6)", source, category, "6");
                SetVariable(variables, "Dice2", "Value of the second die rolled (1-6)", source, category, "6");

                if (eventType != null && eventType.Equals(DefaultCommandEventTypes.RollLose, StringComparison.OrdinalIgnoreCase))
                {
                    // No win amount
                }
                else
                {
                    SetVariable(variables, "WinAmount", "Prize points won for rolling doubles", source, category, "1,000");
                    SetVariable(variables, "PrizeName", "Name of the prize won", source, category, "Boxcars Bonus");
                }
                break;
            }

            case "slot":
            case "slots":
            {
                SetVariable(variables, "Emote1", "The first slot reel emote or symbol", source, category, "Kappa");
                SetVariable(variables, "Emote2", "The second slot reel emote or symbol", source, category, "Kappa");
                SetVariable(variables, "Emote3", "The third slot reel emote or symbol", source, category, "Kappa");

                if (eventType == null || !eventType.Equals(DefaultCommandEventTypes.SlotsLose, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "WinAmount", "Points won from the slot spin", source, category, "1,000");
                    SetVariable(variables, "MatchType", "Match outcome type (e.g. 3 of a kind, 2 of a kind)", source, category, "3 of a kind");
                }
                break;
            }

            case "steal":
            {
                SetVariable(variables, "TargetUser", "Username of the target chatter", source, category, "target_viewer");
                SetVariable(variables, "TargetDisplayName", "Display name of the target chatter", source, category, "Target_Viewer");
                SetVariable(variables, "Amount", "Points stolen, transferred, or given", source, category, "100");
                break;
            }

            case "heist":
            {
                if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistStarted, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "BetAmount", "Starting bet amount entered by the initiator", source, category, "100");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistUserSurvived, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "BetAmount", "Points bet by the surviving participant", source, category, "100");
                    SetVariable(variables, "WinAmount", "Winnings earned from the heist", source, category, "150");
                    SetVariable(variables, "TotalPayout", "Total payout received (BetAmount + WinAmount)", source, category, "250");
                    SetVariable(variables, "TotalParticipants", "Total participants who joined the heist", source, category, "12");
                    SetVariable(variables, "TotalSurvivors", "Total participants who survived the heist", source, category, "8");
                    SetVariable(variables, "TotalCaught", "Total participants caught during the heist", source, category, "4");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistUserCaught, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "LostAmount", "Points lost by the caught participant", source, category, "100");
                    SetVariable(variables, "TotalParticipants", "Total participants who joined the heist", source, category, "12");
                    SetVariable(variables, "TotalSurvivors", "Total participants who survived the heist", source, category, "8");
                    SetVariable(variables, "TotalCaught", "Total participants caught during the heist", source, category, "4");
                }
                else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistEnded, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "TotalParticipants", "Total participants who joined the heist", source, category, "12");
                    SetVariable(variables, "TotalSurvivors", "Total participants who survived the heist", source, category, "8");
                    SetVariable(variables, "TotalCaught", "Total participants caught during the heist", source, category, "4");
                }
                else
                {
                    SetVariable(variables, "BetAmount", "Points bet in the heist", source, category, "100");
                    SetVariable(variables, "WinAmount", "Winnings earned from the heist", source, category, "150");
                    SetVariable(variables, "LostAmount", "Points lost if caught", source, category, "100");
                    SetVariable(variables, "TotalPayout", "Total payout received", source, category, "250");
                    SetVariable(variables, "TotalParticipants", "Total participants who joined the heist", source, category, "12");
                    SetVariable(variables, "TotalSurvivors", "Total participants who survived the heist", source, category, "8");
                    SetVariable(variables, "TotalCaught", "Total participants caught during the heist", source, category, "4");
                }
                break;
            }

            case "death":
            {
                SetVariable(variables, "Game", "Game or category name for the death counter", source, category, "Dark Souls");
                if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DeathReset, StringComparison.OrdinalIgnoreCase))
                {
                    SetVariable(variables, "OldCount", "Previous death counter value before reset", source, category, "42");
                }
                else
                {
                    SetVariable(variables, "NewCount", "Updated death counter value", source, category, "42");
                    SetVariable(variables, "OldCount", "Previous death counter value", source, category, "41");
                }
                break;
            }
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
                    SetVariable(variables, $"counter_{counter.Name}", $"Current value of the '{counter.Name}' counter", source, category, "10");
                }
                if (!string.IsNullOrWhiteSpace(counter.DestinationVariable))
                {
                    SetVariable(variables, counter.DestinationVariable, $"Destination variable containing '{counter.Name}' counter value", source, category, "10");
                }
                break;

            case SetVariableType setVar:
                if (!string.IsNullOrWhiteSpace(setVar.Text))
                {
                    SetVariable(variables, setVar.Text, "Variable assigned by Set Variable subaction", source, category, setVar.Value);
                }
                break;

            case GetGlobalVariableType getGlobalVar:
                var targetName = !string.IsNullOrWhiteSpace(getGlobalVar.TargetVariableName)
                    ? getGlobalVar.TargetVariableName
                    : getGlobalVar.Text;
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    SetVariable(variables, targetName, "Value retrieved from global variable", source, category, "SavedValue");
                }
                break;

            case RandomIntType:
                SetVariable(variables, "random_int", "Generated random integer", source, category, "42");
                break;

            case CurrentTimeType:
                SetVariable(variables, "currentTime", "Current formatted date and time", source, category, DateTime.Now.ToString("g"));
                break;

            case UptimeType:
                SetVariable(variables, "Uptime", "Current stream uptime (e.g. 02:15:30 or Offline)", source, category, "02:15:30");
                break;

            case FollowAgeType:
                SetVariable(variables, "Followage", "Viewer follow duration string", source, category, "2 years, 3 months");
                break;

            case WatchTimeType:
                SetVariable(variables, "watch_time", "Viewer stream watch time string", source, category, "45 hours");
                break;

            case CheckPointsType:
                SetVariable(variables, "TargetPoints", "Loyalty point balance of user", source, category, "1500");
                SetVariable(variables, "TargetPointsFormatted", "Formatted point balance with commas", source, category, "1,500");
                break;

            case GiveawayPrizeType:
                SetVariable(variables, "Prize", "Name of the active giveaway prize", source, category, "Steam Gift Card");
                break;

            case ExternalApiType:
                SetVariable(variables, "ExternalApiResponse", "Raw string response payload from external API", source, category, "{\"status\":\"ok\"}");
                break;

            case FishingType:
                SetVariable(variables, "fish_name", "Name of caught fish", source, category, "Largemouth Bass");
                SetVariable(variables, "fish_weight", "Weight of caught fish", source, category, "5.4");
                SetVariable(variables, "fish_length", "Length of caught fish", source, category, "20.1");
                SetVariable(variables, "fish_value", "Gold value of caught fish", source, category, "220");
                SetVariable(variables, "fish_rarity", "Rarity tier of caught fish", source, category, "Rare");
                break;

            case FishingTournamentEligibleCatchType:
                SetVariable(variables, "fishing_tournament_eligible", "True if catch is eligible for an active tournament", source, category, "true");
                SetVariable(variables, "fishing_tournament_id", "Matching tournament ID", source, category, "1");
                SetVariable(variables, "fishing_tournament_name", "Matching tournament name", source, category, "Derby");
                SetVariable(variables, "fishing_tournament_qualifying", "True if catch placed in qualifying rank", source, category, "true");
                SetVariable(variables, "fishing_tournament_match_count", "Number of matching tournaments", source, category, "1");
                break;

            case RaffleGetEntryCountType:
                SetVariable(variables, "raffle_entry_count", "Total tickets entered by the user in active raffle", source, category, "5");
                break;

            case RaffleEndType:
                SetVariable(variables, "raffle_winner", "Username of the selected raffle winner", source, category, "lucky_viewer");
                SetVariable(variables, "raffle_winner_count", "Number of winners drawn", source, category, "1");
                SetVariable(variables, "raffle_entries_count", "Total eligible tickets in concluded raffle", source, category, "42");
                break;

            case SelectRandomViewersType:
                SetVariable(variables, "SelectedViewerCount", "Total number of viewers chosen", source, category, "3");
                SetVariable(variables, "RandomViewer", "Primary selected random viewer username", source, category, "viewer_one");
                SetVariable(variables, "RandomViewer1", "First selected random viewer username", source, category, "viewer_one");
                SetVariable(variables, "RandomViewer2", "Second selected random viewer username", source, category, "viewer_two");
                break;

            case ForEachViewerType:
                SetVariable(variables, "CurrentViewer", "Username of current viewer in loop iteration", source, category, "current_viewer");
                SetVariable(variables, "ViewerIndex", "0-based iteration index in loop", source, category, "0");
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

    private static void SetVariable(
        Dictionary<string, ActionVariableInfo> variables,
        string name,
        string description,
        string source,
        string category,
        string? exampleValue = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        // Clean any leading/trailing % or whitespace for canonical dictionary key
        name = name.Trim().Trim('%');
        if (string.IsNullOrWhiteSpace(name)) return;

        var effectiveExample = exampleValue ?? (variables.TryGetValue(name, out var existing) ? existing.ExampleValue : null);

        variables[name] = new ActionVariableInfo(
            Name: name,
            Description: description,
            Source: source,
            Category: category,
            ExampleValue: effectiveExample);
    }
}

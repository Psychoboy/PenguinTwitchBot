using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for DefaultCommand triggers across all built-in mini-games and commands.
/// </summary>
public class DefaultCommandTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType == TriggerTypes.DefaultCommand;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();
        var source = sourcePrefix != null ? $"{sourcePrefix} (Default Command: {triggerName})" : $"Trigger: Default Command ({triggerName})";

        // Standard command variables always forwarded on default command executions
        variables.SetVariable("User", "The username of the chatter executing the command", source, category, "penguin_fan");
        variables.SetVariable("UserId", "Twitch user ID when available from the chat event or saved voyage starter; empty for service-generated triggers without an ID", source, category, "12345678");
        variables.SetVariable("Name", "The username of the chatter", source, category, "penguin_fan");
        variables.SetVariable("DisplayName", "The display name of the chatter", source, category, "Penguin_Fan");
        variables.SetVariable("Command", "The name of the default command", source, category, "spinwheel");
        variables.SetVariable("Arg", "Arguments passed after the command or event payload", source, category, "arg1");
        variables.SetVariable("Args", "The arguments passed after the command", source, category, "arg1 arg2");
        variables.SetVariable("TargetUser", "The targeted user (either 1st argument or command invoker)", source, category, "target_viewer");
        variables.SetVariable("IsMod", "True if the chatter is a channel moderator", source, category, "true");
        variables.SetVariable("IsVip", "True if the chatter is a channel VIP", source, category, "false");
        variables.SetVariable("IsSub", "True if the chatter is a channel subscriber", source, category, "true");
        variables.SetVariable("IsBroadcaster", "True if the chatter is the broadcaster", source, category, "false");
        variables.SetVariable("Channel", "The name of the Twitch channel", source, category, "channel_name");

        // Parse or infer command name and event type
        var (commandName, eventType) = ExtractCommandAndEventType(trigger, triggerName);

        // Add command- and event-specific variables
        switch (commandName?.ToLowerInvariant())
        {
            case "spinwheel":
            case "wheel":
                ResolveWheelSpinVariables(variables, source, category);
                break;

            case "gamble":
                ResolveGambleVariables(variables, source, category, eventType);
                break;

            case "defuse":
                ResolveDefuseVariables(variables, source, category, eventType);
                break;

            case "roll":
            case "dice":
                ResolveRollVariables(variables, source, category, eventType);
                break;

            case "slot":
            case "slots":
                ResolveSlotsVariables(variables, source, category, eventType);
                break;

            case "steal":
                ResolveStealVariables(variables, source, category);
                break;

            case "heist":
                ResolveHeistVariables(variables, source, category, eventType);
                break;

            case "death":
                ResolveDeathVariables(variables, source, category, eventType);
                break;

            case "drop":
            case "parachute":
                ResolveParachuteVariables(variables, source, category, eventType);
                break;

            case "plinko":
                ResolvePlinkoVariables(variables, source, category, eventType);
                break;

            case "balloon":
            case "float":
                ResolveBalloonVariables(variables, source, category, eventType);
                break;

            case "curl":
            case "curling":
            case "slide":
                ResolveCurlingVariables(variables, source, category, eventType);
                break;

            case "golf":
            case "putt":
            case "chip":
                ResolveGolfVariables(variables, source, category, eventType);
                break;

            case "starship":
                ResolveStarshipVariables(variables, source, category, eventType);
                break;
        }
    }

    private static (string? CommandName, string? EventType) ExtractCommandAndEventType(TriggerType trigger, string triggerName)
    {
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
            else if (nameLower.Contains("parachute") || nameLower.Contains("drop")) commandName = "drop";
            else if (nameLower.Contains("plinko")) commandName = "plinko";
            else if (nameLower.Contains("balloon") || nameLower.Contains("float")) commandName = "balloon";
            else if (nameLower.Contains("curl") || nameLower.Contains("slide")) commandName = "curl";
            else if (nameLower.Contains("golf") || nameLower.Contains("putt") || nameLower.Contains("chip")) commandName = "golf";
        }

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
            else if (nameLower.Contains("parachute") || nameLower.Contains("drop"))
            {
                if (nameLower.Contains("bullseye"))
                    eventType = DefaultCommandEventTypes.ParachuteBullseye;
                else if (nameLower.Contains("rocket") || nameLower.Contains("kill"))
                    eventType = DefaultCommandEventTypes.ParachuteRocketKill;
                else if (nameLower.Contains("win"))
                    eventType = DefaultCommandEventTypes.ParachuteWin;
                else if (nameLower.Contains("lose") || nameLower.Contains("miss"))
                    eventType = DefaultCommandEventTypes.ParachuteLose;
                else if (nameLower.Contains("start"))
                    eventType = DefaultCommandEventTypes.ParachuteStart;
                else if (nameLower.Contains("end"))
                    eventType = DefaultCommandEventTypes.ParachuteEnd;
            }
            else if (nameLower.Contains("plinko"))
            {
                if (nameLower.Contains("win"))
                    eventType = DefaultCommandEventTypes.PlinkoWin;
                else if (nameLower.Contains("lose"))
                    eventType = DefaultCommandEventTypes.PlinkoLose;
                else if (nameLower.Contains("start"))
                    eventType = DefaultCommandEventTypes.PlinkoStart;
                else if (nameLower.Contains("end"))
                    eventType = DefaultCommandEventTypes.PlinkoEnd;
            }
            else if (nameLower.Contains("balloon") || nameLower.Contains("float"))
            {
                if (nameLower.Contains("bullseye"))
                    eventType = DefaultCommandEventTypes.BalloonBullseye;
                else if (nameLower.Contains("pop"))
                    eventType = DefaultCommandEventTypes.BalloonPop;
                else if (nameLower.Contains("win"))
                    eventType = DefaultCommandEventTypes.BalloonWin;
                else if (nameLower.Contains("lose") || nameLower.Contains("miss"))
                    eventType = DefaultCommandEventTypes.BalloonLose;
                else if (nameLower.Contains("start"))
                    eventType = DefaultCommandEventTypes.BalloonStart;
                else if (nameLower.Contains("end"))
                    eventType = DefaultCommandEventTypes.BalloonEnd;
            }
            else if (nameLower.Contains("curl") || nameLower.Contains("slide"))
            {
                if (nameLower.Contains("bullseye"))
                    eventType = DefaultCommandEventTypes.CurlingBullseye;
                else if (nameLower.Contains("win"))
                    eventType = DefaultCommandEventTypes.CurlingWin;
                else if (nameLower.Contains("lose") || nameLower.Contains("miss"))
                    eventType = DefaultCommandEventTypes.CurlingLose;
                else if (nameLower.Contains("start"))
                    eventType = DefaultCommandEventTypes.CurlingStart;
                else if (nameLower.Contains("end"))
                    eventType = DefaultCommandEventTypes.CurlingEnd;
            }
            else if (nameLower.Contains("golf") || nameLower.Contains("putt") || nameLower.Contains("chip"))
            {
                if (nameLower.Contains("bullseye") || nameLower.Contains("holeinone") || nameLower.Contains("cup"))
                    eventType = DefaultCommandEventTypes.GolfBullseye;
                else if (nameLower.Contains("win"))
                    eventType = DefaultCommandEventTypes.GolfWin;
                else if (nameLower.Contains("lose") || nameLower.Contains("miss"))
                    eventType = DefaultCommandEventTypes.GolfLose;
                else if (nameLower.Contains("start"))
                    eventType = DefaultCommandEventTypes.GolfStart;
                else if (nameLower.Contains("end"))
                    eventType = DefaultCommandEventTypes.GolfEnd;
            }
        }

        return (commandName, eventType);
    }

    private static void ResolveWheelSpinVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category)
    {
        variables.SetVariable("WheelSpinResult", "Winning segment label text of the wheel spin", source, category, "500 Points");
        variables.SetVariable("WinningLabel", "Winning segment label text (alias of WheelSpinResult)", source, category, "500 Points");
        variables.SetVariable("WinningMessage", "Formatted winning announcement message", source, category, "Congratulations! You won 500 Points!");
        variables.SetVariable("WheelName", "Name of the wheel that was spun", source, category, "Prizes Wheel");
        variables.SetVariable("WinningIndex", "Zero-based index of the winning slice", source, category, "2");
        variables.SetVariable("IsNameWheel", "True if the wheel was spun as a viewer name wheel", source, category, "false");
    }

    private static void ResolveGambleVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleJackpotWin, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("JackpotAmount", "Jackpot bonus points won", source, category, "10,000");
            variables.SetVariable("WinAmount", "Base points won from the roll", source, category, "500");
            variables.SetVariable("TotalWinnings", "Total points won (WinAmount + JackpotAmount)", source, category, "10,500");
            variables.SetVariable("RolledValue", "Number rolled on the gamble (1-100)", source, category, "100");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleWin, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("WinAmount", "Points won from the gamble", source, category, "500");
            variables.SetVariable("RolledValue", "Number rolled on the gamble (1-100)", source, category, "95");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.GambleLose, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("LoseAmount", "Points lost from the gamble", source, category, "250");
            variables.SetVariable("RolledValue", "Number rolled on the gamble (1-100)", source, category, "15");
        }
        else
        {
            variables.SetVariable("WinAmount", "Points won from the gamble", source, category, "500");
            variables.SetVariable("LoseAmount", "Points lost from the gamble", source, category, "250");
            variables.SetVariable("JackpotAmount", "Jackpot bonus points won", source, category, "10,000");
            variables.SetVariable("TotalWinnings", "Total points won", source, category, "10,500");
            variables.SetVariable("RolledValue", "Number rolled on the gamble (1-100)", source, category, "95");
        }
    }

    private static void ResolveDefuseVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        variables.SetVariable("ChosenWire", "The wire chosen by the chatter", source, category, "red");
        variables.SetVariable("CorrectWire", "The correct wire to defuse the bomb", source, category, "red");

        if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DefuseSuccess, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("WinAmount", "Reward points awarded for defusing the bomb", source, category, "150");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DefuseFailure, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("LoseAmount", "Points lost when the bomb exploded", source, category, "50");
        }
        else
        {
            variables.SetVariable("WinAmount", "Reward points awarded for defusing the bomb", source, category, "150");
            variables.SetVariable("LoseAmount", "Points lost when the bomb exploded", source, category, "50");
        }
    }

    private static void ResolveRollVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        variables.SetVariable("Dice1", "Value of the first die rolled (1-6)", source, category, "6");
        variables.SetVariable("Dice2", "Value of the second die rolled (1-6)", source, category, "6");

        if (eventType == null || !eventType.Equals(DefaultCommandEventTypes.RollLose, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("WinAmount", "Prize points won for rolling doubles", source, category, "1,000");
            variables.SetVariable("PrizeName", "Name of the prize won", source, category, "Boxcars Bonus");
        }
    }

    private static void ResolveSlotsVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        variables.SetVariable("Emote1", "The first slot reel emote or symbol", source, category, "Kappa");
        variables.SetVariable("Emote2", "The second slot reel emote or symbol", source, category, "Kappa");
        variables.SetVariable("Emote3", "The third slot reel emote or symbol", source, category, "Kappa");

        if (eventType == null || !eventType.Equals(DefaultCommandEventTypes.SlotsLose, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("WinAmount", "Points won from the slot spin", source, category, "1,000");
            variables.SetVariable("MatchType", "Match outcome type (e.g. 3 of a kind, 2 of a kind)", source, category, "3 of a kind");
        }
    }

    private static void ResolveStealVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category)
    {
        variables.SetVariable("TargetUser", "Username of the target chatter", source, category, "target_viewer");
        variables.SetVariable("TargetDisplayName", "Display name of the target chatter", source, category, "Target_Viewer");
        variables.SetVariable("Amount", "Points stolen, transferred, or given", source, category, "100");
    }

    private static void ResolveHeistVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistStarted, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("BetAmount", "Starting bet amount entered by the initiator", source, category, "100");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistUserSurvived, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("BetAmount", "Points bet by the surviving participant", source, category, "100");
            variables.SetVariable("WinAmount", "Winnings earned from the heist", source, category, "150");
            variables.SetVariable("TotalPayout", "Total payout received (BetAmount + WinAmount)", source, category, "250");
            variables.SetVariable("TotalParticipants", "Total participants who joined the heist", source, category, "12");
            variables.SetVariable("TotalSurvivors", "Total participants who survived the heist", source, category, "8");
            variables.SetVariable("TotalCaught", "Total participants caught during the heist", source, category, "4");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistUserCaught, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("LostAmount", "Points lost by the caught participant", source, category, "100");
            variables.SetVariable("TotalParticipants", "Total participants who joined the heist", source, category, "12");
            variables.SetVariable("TotalSurvivors", "Total participants who survived the heist", source, category, "8");
            variables.SetVariable("TotalCaught", "Total participants caught during the heist", source, category, "4");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.HeistEnded, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("TotalParticipants", "Total participants who joined the heist", source, category, "12");
            variables.SetVariable("TotalSurvivors", "Total participants who survived the heist", source, category, "8");
            variables.SetVariable("TotalCaught", "Total participants caught during the heist", source, category, "4");
        }
        else
        {
            variables.SetVariable("BetAmount", "Points bet in the heist", source, category, "100");
            variables.SetVariable("WinAmount", "Winnings earned from the heist", source, category, "150");
            variables.SetVariable("LostAmount", "Points lost if caught", source, category, "100");
            variables.SetVariable("TotalPayout", "Total payout received", source, category, "250");
            variables.SetVariable("TotalParticipants", "Total participants who joined the heist", source, category, "12");
            variables.SetVariable("TotalSurvivors", "Total participants who survived the heist", source, category, "8");
            variables.SetVariable("TotalCaught", "Total participants caught during the heist", source, category, "4");
        }
    }

    private static void ResolveDeathVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        variables.SetVariable("Game", "Game or category name for the death counter", source, category, "Dark Souls");
        if (eventType != null && eventType.Equals(DefaultCommandEventTypes.DeathReset, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("OldCount", "Previous death counter value before reset", source, category, "42");
        }
        else
        {
            variables.SetVariable("NewCount", "Updated death counter value", source, category, "42");
            variables.SetVariable("OldCount", "Previous death counter value", source, category, "41");
        }
    }

    private static void ResolveParachuteVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.ParachuteStart, StringComparison.OrdinalIgnoreCase) ||
                                  eventType.Equals(DefaultCommandEventTypes.ParachuteStarted, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("Duration", "Total duration of the parachute session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the parachute session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "ParachuteDrop");
        }
        else if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.ParachuteEnd, StringComparison.OrdinalIgnoreCase) ||
                                       eventType.Equals(DefaultCommandEventTypes.ParachuteEnded, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("TotalDrops", "Total number of parachutists who dropped during the round", source, category, "42");
            variables.SetVariable("Duration", "Total duration of the parachute session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started or stopped the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the parachute session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "ParachuteDrop");
        }
        else if (eventType != null && eventType.Equals(DefaultCommandEventTypes.ParachuteRocketKill, StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("Attacker", "Username or system that fired the rocket", source, category, "RocketSniper");
            variables.SetVariable("TriggeredBy", "Username or system that fired the rocket", source, category, "RocketSniper");
            variables.SetVariable("Victim", "Chatter whose parachute was destroyed", source, category, "victim_viewer");
            variables.SetVariable("Score", "Landing accuracy score (or preserved crash score)", source, category, "100");
            variables.SetVariable("ParachuteDestroyed", "True if the canopy was destroyed by a rocket", source, category, "true");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "ParachuteDrop");
        }
        else
        {
            variables.SetVariable("Score", "Landing accuracy score achieved (0 - 100)", source, category, "100");
            variables.SetVariable("IsWin", "True if the parachute drop landed in a winning target zone", source, category, "true");
            variables.SetVariable("IsBullseye", "True if the parachute drop landed dead-center on the bullseye", source, category, "true");
            variables.SetVariable("DropletHits", "Number of interactive droplets that collided with the parachutist mid-air", source, category, "2");
            variables.SetVariable("DistanceFromCenter", "Distance from the target center in percentage points", source, category, "1.2");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Spring (Cherry Blossoms)");
        }
    }

    private static void ResolvePlinkoVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.PlinkoStart, StringComparison.OrdinalIgnoreCase) ||
                                  eventType.Equals(DefaultCommandEventTypes.PlinkoStarted, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("Duration", "Total duration of the plinko session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the plinko session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "PlinkoBounce");
        }
        else if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.PlinkoEnd, StringComparison.OrdinalIgnoreCase) ||
                                       eventType.Equals(DefaultCommandEventTypes.PlinkoEnded, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("TotalDrops", "Total number of plinko tokens dropped during the round", source, category, "35");
            variables.SetVariable("TotalTokens", "Total number of plinko tokens dropped during the round", source, category, "35");
            variables.SetVariable("Duration", "Total duration of the plinko session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started or stopped the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the plinko session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "PlinkoBounce");
        }
        else
        {
            variables.SetVariable("Score", "Points/multiplier value of the Plinko bucket landed in (0 for winner-only mode)", source, category, "0");
            variables.SetVariable("BucketLabel", "Display label of the Plinko bucket landed in", source, category, "WINNER");
            variables.SetVariable("BucketIndex", "Zero-based index of the Plinko bucket landed in", source, category, "2");
            variables.SetVariable("IsWin", "True if the Plinko bucket is marked as a winning bucket", source, category, "true");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Spring (Cherry Blossoms)");
        }
    }

    private static void ResolveBalloonVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.BalloonStart, StringComparison.OrdinalIgnoreCase) ||
                                  eventType.Equals(DefaultCommandEventTypes.BalloonStarted, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("Duration", "Total duration of the balloon session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the balloon session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "BalloonAscent");
        }
        else if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.BalloonEnd, StringComparison.OrdinalIgnoreCase) ||
                                       eventType.Equals(DefaultCommandEventTypes.BalloonEnded, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("TotalDrops", "Total number of balloons launched during the round", source, category, "28");
            variables.SetVariable("Duration", "Total duration of the balloon session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started or stopped the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the balloon session", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "BalloonAscent");
        }
        else
        {
            variables.SetVariable("Score", "Sky arch accuracy score achieved (0 - 100)", source, category, "100");
            variables.SetVariable("IsWin", "True if the balloon landed in the sky arch target zone", source, category, "true");
            variables.SetVariable("IsBullseye", "True if the balloon hit the center bullseye of the sky arch", source, category, "true");
            variables.SetVariable("BalloonPopped", "True if the balloon was popped mid-air by falling hazards", source, category, "false");
            variables.SetVariable("DistanceFromCenter", "Distance from the sky arch center in percentage points", source, category, "1.5");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Spring (Cherry Blossoms)");
            variables.SetVariable("Game", "Game name", source, category, "BalloonAscent");
        }
    }

    private static void ResolveCurlingVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.CurlingStart, StringComparison.OrdinalIgnoreCase) ||
                                  eventType.Equals(DefaultCommandEventTypes.CurlingStarted, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("Duration", "Total duration of the curling session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the curling session", source, category, "Winter (Snowflakes)");
            variables.SetVariable("Condition", "Ice condition used for the curling round", source, category, "Slick Ice");
            variables.SetVariable("TargetDistance", "Target distance used for the curling round", source, category, "84.0");
            variables.SetVariable("Game", "Game name", source, category, "Curling");
        }
        else if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.CurlingEnd, StringComparison.OrdinalIgnoreCase) ||
                                       eventType.Equals(DefaultCommandEventTypes.CurlingEnded, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("TotalDrops", "Total number of curling stones slid during the round", source, category, "20");
            variables.SetVariable("Duration", "Total duration of the curling session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started or stopped the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the curling session", source, category, "Winter (Snowflakes)");
            variables.SetVariable("Game", "Game name", source, category, "Curling");
        }
        else
        {
            variables.SetVariable("Score", "Curling house ring score achieved (-10 to 100)", source, category, "100");
            variables.SetVariable("Power", "Launch power used for the slide (1 - 100)", source, category, "80");
            variables.SetVariable("IsWin", "True if the stone stopped inside a scoring house ring", source, category, "true");
            variables.SetVariable("IsBullseye", "True if the stone stopped dead-center on the button (100 pts)", source, category, "true");
            variables.SetVariable("DistanceFromCenter", "Distance from the house center in percentage points", source, category, "0.8");
            variables.SetVariable("Condition", "Ice condition used for this slide", source, category, "Slick Ice");
            variables.SetVariable("IceCondition", "Ice condition used for this slide", source, category, "Slick Ice");
            variables.SetVariable("TargetDistance", "Target distance used for this slide", source, category, "84.0");
            variables.SetVariable("TargetDistancePercent", "Target distance as a percent of track length", source, category, "84.0");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Winter (Snowflakes)");
            variables.SetVariable("Game", "Game name", source, category, "Curling");
        }
    }

    private static void ResolveGolfVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.GolfStart, StringComparison.OrdinalIgnoreCase) ||
                                  eventType.Equals(DefaultCommandEventTypes.GolfStarted, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("Duration", "Total duration of the golf session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the golf session", source, category, "Summer (Beach Party)");
            variables.SetVariable("Condition", "Green condition used for the golf round", source, category, "Fast Links");
            variables.SetVariable("TargetDistance", "Pin distance used for the golf round", source, category, "84.0");
            variables.SetVariable("Game", "Game name", source, category, "Golf");
        }
        else if (eventType != null && (eventType.Equals(DefaultCommandEventTypes.GolfEnd, StringComparison.OrdinalIgnoreCase) ||
                                       eventType.Equals(DefaultCommandEventTypes.GolfEnded, StringComparison.OrdinalIgnoreCase)))
        {
            variables.SetVariable("TotalDrops", "Total number of golf shots taken during the round", source, category, "24");
            variables.SetVariable("Duration", "Total duration of the golf session in seconds", source, category, "100");
            variables.SetVariable("TriggeredBy", "Username or system that started or stopped the session", source, category, "Broadcaster");
            variables.SetVariable("Theme", "Active theme name of the golf session", source, category, "Summer (Beach Party)");
            variables.SetVariable("Game", "Game name", source, category, "Golf");
        }
        else
        {
            variables.SetVariable("Score", "Final golf score after multi-stroke penalty (0 - 100)", source, category, "100");
            variables.SetVariable("BaseScore", "Closest-to-the-pin base score before stroke penalty (0 - 100)", source, category, "100");
            variables.SetVariable("StrokeNumber", "Stroke number used by the player on this hole", source, category, "1");
            variables.SetVariable("StrokePenalty", "Points deducted for additional strokes", source, category, "0");
            variables.SetVariable("Power", "Swing power used for the golf shot (1 - 100)", source, category, "84");
            variables.SetVariable("IsWin", "True if the ball rested on the putting green or was sunk in the cup", source, category, "true");
            variables.SetVariable("IsBullseye", "True if the ball was sunk in the cup", source, category, "true");
            variables.SetVariable("IsHoleInOne", "True if the ball was sunk in the cup on stroke 1", source, category, "true");
            variables.SetVariable("IsLipOut", "True if the ball hit the cup rim too fast and lipped out", source, category, "false");
            variables.SetVariable("DistanceFromCenter", "Distance from the pin in percentage points", source, category, "0.4");
            variables.SetVariable("Condition", "Green condition used for this golf shot", source, category, "Fast Links");
            variables.SetVariable("GreenCondition", "Green condition used for this golf shot", source, category, "Fast Links");
            variables.SetVariable("TargetDistance", "Pin distance used for this golf shot", source, category, "84.0");
            variables.SetVariable("PinDistance", "Pin distance used for this golf shot", source, category, "84.0");
            variables.SetVariable("TargetDistancePercent", "Pin distance as a percent of strip length", source, category, "84.0");
            variables.SetVariable("Theme", "Active seasonal or custom theme name", source, category, "Summer (Beach Party)");
            variables.SetVariable("Game", "Game name", source, category, "Golf");
        }
    }

    private static void ResolveStarshipVariables(Dictionary<string, ActionVariableInfo> variables, string source, string category, string? eventType)
    {
        variables.SetVariable("Game", "Game name", source, category, "Starship Expedition");
        variables.SetVariable("ExpeditionId", "Unique ID of the voyage", source, category, "00000000-0000-0000-0000-000000000000");
        variables.SetVariable("Leg", "Current voyage leg", source, category, "1");
        variables.SetVariable("LegCount", "Total configured voyage legs", source, category, "5");
        variables.SetVariable("Sector", "Current route sector", source, category, "Kepler Reach");
        variables.SetVariable("CrewCount", "Number of viewers aboard", source, category, "4");
        variables.SetVariable("StartedBy", "Viewer who started the voyage", source, category, "Broadcaster");
        variables.SetVariable("ShipName", "Name of the expedition ship", source, category, "Rockhopper Voyager");
        variables.SetVariable("Route", "Current voyage route", source, category, "Survey Route");
        variables.SetVariable("VoyageStatus", "Boarding, Cruising, EventOpen, Returned or Aborted", source, category, "Cruising");
        variables.SetVariable("Seed", "Saved deterministic voyage seed", source, category, "12345");
        variables.SetVariable("CompletedLegs", "Number of completed voyage legs", source, category, "2");
        variables.SetVariable("NextEventAtUtc", "Next scheduled event time (ISO UTC; empty when unscheduled)", source, category, "2026-10-23T12:30:00Z");
        variables.SetVariable("BoardingClosesAtUtc", "Boarding closing time (ISO UTC; may be empty)", source, category, "2026-10-23T12:00:00Z");
        variables.SetVariable("ChatMessage", "Full built-in chat announcement for this transition; empty when no message is sent. Sending it again duplicates the built-in announcement.", source, category, "Derelict Salvage: SUCCESS. Earned 15 Credits average per eligible crew member.");
        variables.SetVariable("TtsMessage", "Concise spoken summary for a TTS sub-action, without vote commands, detailed odds, IDs or signed resource notation. Providing the variable does not speak it automatically.", source, category, "Derelict Salvage succeeded. The find was Rare. Average reward per eligible crew member: 15 Credits.");
        foreach (var (name, description, example) in new[]
        {
            ("Winnings", "Earned amounts with each currency kept separate", "60 Credits, 20 Tokens"),
            ("Losses", "Lost amounts with each currency kept separate", "10 Credits"),
            ("WinningsPerCrewMember", "Average earned per distinct eligible crew member for each currency, including zero awards", "15 Credits, 5 Tokens"),
            ("LossesPerCrewMember", "Average lost per distinct eligible crew member for each currency", "2.5 Credits"),
            ("PointTypes", "Currency names represented by outcome rows", "Credits, Tokens"),
            ("RewardAmountsJson", "JSON array with pointTypeId, pointTypeName, earned, lost and crewCount; amounts are totals, averages are total divided by crewCount", "[{\"pointTypeId\":1,\"pointTypeName\":\"Credits\",\"earned\":60,\"lost\":0,\"crewCount\":4}]")
        })
            variables.SetVariable("Voyage" + name, "Whole voyage: " + description, source, category, example);
        variables.SetVariable("BankedAmounts", "Already-paid permanent amounts per currency for this voyage", source, category, "50 Credits");
        variables.SetVariable("PendingBankedAmounts", "Queued but unpaid banking amounts per currency", source, category, "10 Credits");
        variables.SetVariable("UnbankedAmounts", "Current crew unbanked balances per currency", source, category, "25 Credits");
        variables.SetVariable("ShipSystemsEnabled", "Whether this voyage uses shared ship resources", source, category, "true");
        variables.SetVariable("Hull", "Current shared hull (empty on legacy/disabled voyages)", source, category, "85");
        variables.SetVariable("HullMaximum", "Maximum shared hull for this voyage", source, category, "100");
        variables.SetVariable("Supplies", "Current shared supplies (empty on legacy/disabled voyages)", source, category, "60");
        variables.SetVariable("PendingSuccessBonusPercent", "Next-event success bonus in percentage points", source, category, "10");
        variables.SetVariable("ActiveSuccessBonusPercent", "Currently active success bonus in percentage points", source, category, "0");
        variables.SetVariable("ReturnReason", "HullDepleted for an emergency return; otherwise empty", source, category, "HullDepleted");
        if (eventType is DefaultCommandEventTypes.StarshipEventResolved or DefaultCommandEventTypes.StarshipEventSucceeded or DefaultCommandEventTypes.StarshipEventFailed)
        {
            variables.SetVariable("ChoiceId", "Winning/default choice ID", source, category, "salvage");
            variables.SetVariable("Choice", "Winning/default choice label", source, category, "Recover salvage");
            variables.SetVariable("VoteCount", "Crew votes recorded for the resolved event", source, category, "4");
            variables.SetVariable("EligibleCrewCount", "Distinct earn-time eligible crew members", source, category, "4");
            variables.SetVariable("RewardParticipantCount", "Distinct crew members earning points", source, category, "3");
            variables.SetVariable("Summary", "Detailed outcome summary including story/path and resource changes", source, category, "Derelict Salvage resolved. SUCCESS - Rare find.");
            variables.SetVariable("Winnings", "Event total winnings, separately labeled per currency", source, category, "60 Credits, 20 Tokens");
            variables.SetVariable("Losses", "Event total losses, separately labeled per currency", source, category, "10 Credits");
            variables.SetVariable("WinningsPerCrewMember", "Average earned per eligible crew member by currency, including zero awards", source, category, "15 Credits, 5 Tokens");
            variables.SetVariable("LossesPerCrewMember", "Average lost per eligible crew member by currency", source, category, "2.5 Credits");
            variables.SetVariable("PointTypes", "All event outcome currencies", source, category, "Credits, Tokens");
            variables.SetVariable("RewardAmountsJson", "Per-currency JSON array: pointTypeId, pointTypeName, earned, lost, crewCount", source, category, "[{\"pointTypeId\":1,\"pointTypeName\":\"Credits\",\"earned\":60,\"lost\":0,\"crewCount\":4}]");
            variables.SetVariable("HullBefore", "Hull at event opening, after travel", source, category, "100");
            variables.SetVariable("HullMaximum", "Maximum shared hull for this voyage", source, category, "100");
            variables.SetVariable("HullAfter", "Hull after all event steps and outcome effects", source, category, "85");
            variables.SetVariable("HullChange", "Net hull change across the whole event", source, category, "-15");
            variables.SetVariable("SuppliesBefore", "Supplies at event opening, after travel", source, category, "65");
            variables.SetVariable("SuppliesAfter", "Supplies after all event steps and outcome effects", source, category, "60");
            variables.SetVariable("SuppliesChange", "Net supplies change across the whole event (excluding travel)", source, category, "-5");
            variables.SetVariable("EventBaseSuccessChancePercent", "Frozen base chance before resource and choice modifiers", source, category, "80");
            variables.SetVariable("EventSuccessModifierPercent", "Frozen shortage and Continue modifier in percentage points", source, category, "-15");
        }

        if (eventType is DefaultCommandEventTypes.StarshipEventOpened or DefaultCommandEventTypes.StarshipEventStepAdvanced or
            DefaultCommandEventTypes.StarshipEventResolved or DefaultCommandEventTypes.StarshipEventSucceeded or DefaultCommandEventTypes.StarshipEventFailed)
        {
            variables.SetVariable("EventId", "Stable event ID", source, category, "event-id");
            variables.SetVariable("EventDescription", "Original event description template", source, category, "A cache near {subject}.");
            variables.SetVariable("Step", "Current/resolved step number", source, category, "2");
            variables.SetVariable("StepId", "Current/resolved graph step ID", source, category, "recovery");
        }
        if (eventType is DefaultCommandEventTypes.StarshipEventOpened or DefaultCommandEventTypes.StarshipEventStepAdvanced)
        {
            variables.SetVariable("DefaultChoiceId", "Safe timeout/default choice ID", source, category, "pass");
            variables.SetVariable("EventClosesAtUtc", "Current vote deadline in ISO UTC", source, category, "2026-10-23T12:30:00Z");
            variables.SetVariable("VoteCount", "Votes currently recorded", source, category, "0");
            variables.SetVariable("Duration", "Current vote window in seconds", source, category, "60");
        }

        if (eventType == DefaultCommandEventTypes.StarshipBoardingOpened)
        {
            variables.SetVariable("TriggeredBy", "Broadcaster or moderator who opened boarding", source, category, "Broadcaster");
            variables.SetVariable("Duration", "Crew boarding window in seconds", source, category, "60");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipCrewJoined)
        {
            variables.SetVariable("User", "Crew member who joined", source, category, "Penguin_Fan");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipEventOpened)
        {
            variables.SetVariable("EventTitle", "Title of the opened crew event", source, category, "Unidentified Signal");
            variables.SetVariable("EventType", "Kind of the opened crew event", source, category, "SignalDetected");
            variables.SetVariable("Subject", "Generated name of the ship, signal or hazard", source, category, "the Kestrel-7 beacon");
            variables.SetVariable("Choices", "Labels of the available event choices", source, category, "Scan the signal | Trace its origin | Continue on course");
            variables.SetVariable("Duration", "Crew vote window in seconds", source, category, "60");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipEventResolved)
        {
            variables.SetVariable("EventTitle", "Title of the resolved crew event", source, category, "Unidentified Signal");
            variables.SetVariable("EventType", "Kind of the resolved crew event", source, category, "SignalDetected");
            variables.SetVariable("Choice", "Winning/default choice label", source, category, "Scan the signal");
            variables.SetVariable("VoteCount", "Number of crew votes recorded", source, category, "3");
            variables.SetVariable("PointType", "Point currency used by the event reward profile", source, category, "Salvage Credits");
            variables.SetVariable("RewardParticipantCount", "Eligible crew members who earned a reward", source, category, "3");
            variables.SetVariable("RewardTotal", "Aggregate unbanked reward amount for the event", source, category, "45");
            variables.SetVariable("Outcome", "Event outcome: Success, Failure or Passed; independent of point rewards on integrated voyages", source, category, "Success");
            variables.SetVariable("Rarity", "Rarity of the find rolled for the event", source, category, "Uncommon");
            variables.SetVariable("Path", "Choices the crew made, in order", source, category, "Scan the wreck > Secure the area > Recover salvage");
            variables.SetVariable("LostTotal", "Aggregate unbanked points the crew lost on failure", source, category, "0");
            variables.SetVariable("Summary", "Ready-made outcome sentence for chat", source, category, "Derelict Salvage resolved. SUCCESS - Uncommon find.");
            variables.SetVariable("Story", "Generated story of what happened, using viewer names", source, category, "Penguin_Fan boards the ISV Quiet Meridian and starts cutting toward the cargo hold.");
            variables.SetVariable("Subject", "Generated name of the ship, signal or hazard", source, category, "the ISV Quiet Meridian");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipEventStepAdvanced)
        {
            variables.SetVariable("EventTitle", "Title of the multi-step event", source, category, "Derelict Salvage");
            variables.SetVariable("EventType", "Kind of the event", source, category, "SalvageSite");
            variables.SetVariable("Step", "Step the event just moved to", source, category, "2");
            variables.SetVariable("Subject", "Generated name of the ship, signal or hazard", source, category, "the ISV Quiet Meridian");
            variables.SetVariable("Path", "Choices the crew made so far, in order", source, category, "Scan the wreck");
            variables.SetVariable("RevealedRarity", "Rarity revealed by a scan (empty until scanned)", source, category, "Rare");
            variables.SetVariable("StepNote", "What the last choice did", source, category, "Scan complete: this is a Rare find.");
            variables.SetVariable("Choices", "Numbered choices available in the new step", source, category, "1) Secure the area 2) Recover salvage 3) Pass it by");
        }
        else if (eventType is DefaultCommandEventTypes.StarshipEventSucceeded or DefaultCommandEventTypes.StarshipEventFailed)
        {
            variables.SetVariable("EventTitle", "Title of the finished event", source, category, "Derelict Salvage");
            variables.SetVariable("EventType", "Kind of the finished event", source, category, "SalvageSite");
            variables.SetVariable("Outcome", "Event outcome", source, category,
                eventType == DefaultCommandEventTypes.StarshipEventSucceeded ? "Success" : "Failure");
            variables.SetVariable("Rarity", "Rarity of the find", source, category, "Uncommon");
            variables.SetVariable("Path", "Choices the crew made, in order", source, category, "Scan the wreck > Secure the area > Recover salvage");
            variables.SetVariable("PointType", "Point currency used by the event reward profile", source, category, "Salvage Credits");
            variables.SetVariable("RewardTotal", "Aggregate unbanked reward amount", source, category, "45");
            variables.SetVariable("LostTotal", "Aggregate unbanked points lost by the crew", source, category, "0");
            variables.SetVariable("AffectedCount", "Crew members who earned or lost points", source, category, "4");
            variables.SetVariable("Story", "Generated story of what happened, using viewer names", source, category, "Penguin_Fan boards the ISV Quiet Meridian and starts cutting toward the cargo hold. The hatch gives way and the crew hauls out an intact cryo-vault.");
            variables.SetVariable("Subject", "Generated name of the ship, signal or hazard", source, category, "the ISV Quiet Meridian");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipVoyageEnded)
        {
            variables.SetVariable("Result", "Voyage terminal result", source, category, "Returned");
            variables.SetVariable("Duration", "Voyage duration in seconds", source, category, "3600");
            variables.SetVariable("TriggeredBy", "Broadcaster or moderator who ended the voyage", source, category, "Broadcaster");
            variables.SetVariable("Summary", "Ready-made voyage summary for chat", source, category, "Voyage completed: 5 events, 3 successful, 1 failed, 4 crew. Earned 120 Salvage Credits.");
            variables.SetVariable("EventsSucceeded", "Events whose outcome roll succeeded", source, category, "3");
            variables.SetVariable("EventsFailed", "Events whose outcome roll failed", source, category, "1");
        }
        else if (eventType == DefaultCommandEventTypes.StarshipCrewRewardBanked)
        {
            variables.SetVariable("PointTypeId", "Banked currency's numeric ID", source, category, "1");
            variables.SetVariable("AmountFormatted", "Banked amount with local thousands separators", source, category, "1,000");
            variables.SetVariable("OperationId", "Stable idempotent payout operation ID", source, category, "starship:bank:operation");
            variables.SetVariable("PointType", "Point currency banked for this crew member", source, category, "Salvage Credits");
            variables.SetVariable("Amount", "Amount added to the crew member's permanent balance", source, category, "25");
            variables.SetVariable("EventId", "Station event that settled the payout", source, category, "event-id");
        }
        else if (eventType is DefaultCommandEventTypes.StarshipRiskSucceeded or DefaultCommandEventTypes.StarshipRiskFailed)
        {
            variables.SetVariable("EventTitle", "Risk event the crew member chose", source, category, "Derelict Salvage");
            variables.SetVariable("EventType", "Kind of the risk event", source, category, "SalvageSite");
            variables.SetVariable("PointType", "Point currency exposed to this action", source, category, "Salvage Credits");
            variables.SetVariable("EarnedAmount", "Unbanked reward earned from success", source, category, "20");
            variables.SetVariable("LostAmount", "Unbanked balance lost on failure", source, category, "10");
            variables.SetVariable("Rarity", "Reward rarity selected on success", source, category, "Uncommon");
            variables.SetVariable("Choice", "Risky action chosen by this crew member", source, category, "Recover salvage");
            variables.SetVariable("SuccessChancePercent", "Configured probability of success for this risky action", source, category, "75");
            variables.SetVariable("Outcome", "Individual risky-action outcome", source, category,
                eventType == DefaultCommandEventTypes.StarshipRiskSucceeded ? "Success" : "Failure");
        }
    }
}


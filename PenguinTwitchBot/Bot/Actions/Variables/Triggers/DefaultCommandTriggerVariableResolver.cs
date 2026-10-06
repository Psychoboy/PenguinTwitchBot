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
}


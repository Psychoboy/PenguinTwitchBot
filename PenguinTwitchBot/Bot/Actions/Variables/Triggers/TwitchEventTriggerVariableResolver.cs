using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for TwitchEvent triggers (Bits, Raid, Sub, Channel Points, Ad Break, Ban, Stream Online/Offline).
/// </summary>
public class TwitchEventTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType == TriggerTypes.TwitchEvent;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();
        var source = sourcePrefix != null ? $"{sourcePrefix} (Event: {triggerName})" : $"Trigger: Twitch Event ({triggerName})";

        variables.SetVariable("User", "The user initiating or targeted in the event", source, category, "chatter_name");
        variables.SetVariable("UserName", "The login username of the chatter", source, category, "chatter_name");
        variables.SetVariable("DisplayName", "The display name of the chatter", source, category, "Chatter_Name");

        var eventType = triggerName;

        if (eventType.Contains("Cheer", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Bits", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("Bits", "Total bits cheered or spent", source, category, "500");
            variables.SetVariable("Message", "Cheer message content", source, category, "Cheer500 Great stream!");
        }
        else if (eventType.Contains("Raid", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("Viewers", "Count of raiding viewers joining the stream", source, category, "42");
        }
        else if (eventType.Contains("Subscribe", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Subscription", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("Tier", "Subscription tier (Tier 1, Tier 2, Tier 3, Prime)", source, category, "Tier 1");
            variables.SetVariable("Months", "Cumulative total months subscribed", source, category, "6");
            variables.SetVariable("Streak", "Consecutive subscription streak in months", source, category, "3");
            variables.SetVariable("Message", "Resubscription message content", source, category, "Love the content!");
            variables.SetVariable("Total", "Total gift subscriptions in this bundle", source, category, "5");
            variables.SetVariable("IsGift", "True if this subscription was gifted", source, category, "true");
        }
        else if (eventType.Contains("Point", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Reward", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("RewardTitle", "Title of the redeemed Channel Points custom reward", source, category, "Hydrate");
            variables.SetVariable("RewardCost", "Point cost of the custom reward", source, category, "250");
            variables.SetVariable("UserInput", "Viewer text entered when redeeming the reward", source, category, "Take a sip!");
            variables.SetVariable("RedemptionId", "Twitch redemption UUID", source, category, "uuid-1234");
        }
        else if (eventType.Contains("AdBreak", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("DurationSeconds", "Length of the ad break in seconds", source, category, "90");
            variables.SetVariable("IsAutomatic", "True if ad break was scheduled automatically", source, category, "false");
        }
        else if (eventType.Contains("Ban", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("Reason", "Reason specified for the ban or timeout", source, category, "Spamming");
            variables.SetVariable("DurationSeconds", "Timeout duration in seconds (blank or 0 for permanent ban)", source, category, "600");
        }
        else if (eventType.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("StreamStartedAt", "Timestamp when broadcast went live", source, category, DateTime.UtcNow.ToString("O"));
        }
        else if (eventType.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            variables.SetVariable("StreamDuration", "Total length of the broadcast session", source, category, "03:45:12");
        }
    }
}

